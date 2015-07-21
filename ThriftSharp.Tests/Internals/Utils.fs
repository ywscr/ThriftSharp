﻿// Copyright (c) 2014-15 Solal Pirelli
// This code is licensed under the MIT License (see Licence.txt for details).

[<AutoOpen>]
module ThriftSharp.Tests.Utils

open System
open System.Collections.Generic
open System.Reflection
open System.Reflection.Emit
open System.Threading
open System.Threading.Tasks
open Microsoft.FSharp.Quotations.Patterns
open Linq.QuotationEvaluation
open Xunit
open ThriftSharp
open ThriftSharp.Internals

// Use Nullable without having to open System (because of conflicts with e.g. Int32 in memory protocol)
let nullable x = 
    Nullable(x)


/// Nicer syntax for assert equals, using deep equality
let (<=>) (act: 'a) (exp: 'a) =
    DeepEqual.Syntax.ObjectExtensions.ShouldDeepEqual(act, exp)


/// Simple computation expression to support Tasks, in the same way as F#'s AsyncBuilder
type TaskBuilder() =
    member x.Zero() =
        Task.CompletedTask
        
    member x.Bind(t: Task, f: unit -> Task): Task =
        t.ContinueWith(fun _ -> f()).Unwrap()

    member x.Bind(t: Task<'T>, f: 'T -> Task<'U>): Task<'U> =
        t.ContinueWith(fun (x: Task<_>) -> f x.Result).Unwrap()

let task = TaskBuilder()


/// Helper type because CustomAttributeBuilder is annoying
[<NoComparison>]
type AttributeInfo = 
    { typ: Type
      args: obj list
      namedArgs: (string * obj) list }

let asBuilder (ai: AttributeInfo) =
    CustomAttributeBuilder(ai.typ.GetConstructors() |> Array.head, 
                           ai.args |> List.toArray, 
                           ai.namedArgs |> List.map (fst >> ai.typ.GetProperty) |> List.toArray, 
                           ai.namedArgs |> List.map snd |> List.toArray)

/// Creates an interface with the specified attributes and methds (parameters and attrs, return type, attrs)
let makeInterface (attrs: AttributeInfo list) 
                  (meths: ((Type * AttributeInfo list) list * Type * AttributeInfo list) list) =    
    let guid = Guid.NewGuid()
    let assemblyName = AssemblyName(guid.ToString())
    let moduleBuilder = Thread.GetDomain()
                              .DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run)
                              .DefineDynamicModule(assemblyName.Name)
    
    let interfaceBuilder = moduleBuilder.DefineType("GeneratedType", TypeAttributes.Interface ||| TypeAttributes.Abstract ||| TypeAttributes.Public)
    
    attrs |> List.map asBuilder |> List.iter interfaceBuilder.SetCustomAttribute

    meths |> List.iteri (fun n (args, retType, methAttrs) ->
        let methodBuilder = interfaceBuilder.DefineMethod(string n, 
                                                          MethodAttributes.Public ||| MethodAttributes.Abstract ||| MethodAttributes.Virtual, 
                                                          retType, 
                                                          args |> List.map fst |> List.toArray)

        args |> List.iteri (fun i (typ, argAttrs) ->
             // i + 1 because 0 is the return value
            let paramBuilder = methodBuilder.DefineParameter(i + 1, ParameterAttributes.None, string i)
            argAttrs |> List.map asBuilder |> List.iter paramBuilder.SetCustomAttribute )

        methAttrs |> List.map asBuilder |> List.iter methodBuilder.SetCustomAttribute )
    
    interfaceBuilder.CreateType().GetTypeInfo()




let tid (n: int) = byte n |> LanguagePrimitives.EnumOfValue

let date(dd, mm, yyyy) = 
    let d = DateTime(yyyy, mm, dd, 00, 00, 00, DateTimeKind.Utc)
    d.ToLocalTime()

let dict (vals: ('a * 'b) seq) =
    let dic = Dictionary()
    for (k,v) in vals do
        dic.Add(k, v)
    dic

let throws<'T when 'T :> exn> func =
    let exn = ref Unchecked.defaultof<'T>

    try
        func() |> ignore
    with
    | ex when typeof<'T>.IsAssignableFrom(ex.GetType()) -> 
        exn := ex :?> 'T
    | ex -> 
        Assert.True(false, sprintf "Expected an exception of type %A, but got one of type %A (message: %s)" typeof<'T> (ex.GetType()) ex.Message)
    
    if Object.Equals(!exn, null) then
        Assert.True(false, "Expected an exception, but none was thrown.")
    !exn

let throwsAsync<'T when 'T :> exn> (func: Async<obj>) = 
    Async.FromContinuations(fun (cont, econt, _) ->
        Async.StartWithContinuations(
            func,
            (fun _ -> econt(Exception("Expected an exception, but none was thrown."))),
            (fun e -> match (match e with :? AggregateException as e -> e.InnerException | _ -> e) with
                      | e when typeof<'T>.IsAssignableFrom(e.GetType()) -> 
                          cont (e :?> 'T)
                      | e ->
                          econt(Exception(sprintf "Expected an %A, got an %A (message: %s)" typeof<'T> (e.GetType()) e.Message))),
            (fun e -> if typeof<'T> <> typeof<OperationCanceledException> then
                          econt(Exception(sprintf "Expected an %A, got an OperationCanceledException." typeof<'T>))
                      else
                          cont (box e :?> 'T))
        )
    )

let run x = x |> Async.Ignore |> Async.RunSynchronously

let read<'T> prot =
    let thriftStruct = ThriftAttributesParser.ParseStruct(typeof<'T>.GetTypeInfo())
    ThriftStructReader.Read<'T>(thriftStruct, prot)

let write prot obj =
    let thriftStruct = ThriftAttributesParser.ParseStruct(obj.GetType().GetTypeInfo())
    let meth = typeof<ThriftStructWriter>.GetMethod("Write").MakeGenericMethod([| obj.GetType() |])
    try
        meth.Invoke(null, [| thriftStruct; obj; prot |]) |> ignore
    with
    | :? TargetInvocationException as e -> raise e.InnerException

let readMsgAsync<'T> prot name =
    let svc = ThriftAttributesParser.ParseService(typeof<'T>.GetTypeInfo())
    Thrift.CallMethodAsync<obj>(ThriftCommunication(prot), svc, name, [| |]) |> Async.AwaitTask


let writeMsgAsync<'T> methodName args = async {
    let m = MemoryProtocol([MessageHeader ("", ThriftMessageType.Reply)
                            StructHeader ""
                            FieldStop
                            StructEnd
                            MessageEnd])
    let svc = ThriftAttributesParser.ParseService(typeof<'T>.GetTypeInfo())
    do! Thrift.CallMethodAsync<obj>(ThriftCommunication(m), svc, methodName, args) |> Async.AwaitTask |> Async.Ignore
    return m
}

let makeClass structAttrs propsAndAttrs =
    let ctorAndArgs = function
        | NewObject (ctor, args) -> ctor, (args |> Array.ofList |> Array.map (fun a -> a.EvalUntyped()))
        | _ -> failwith "not a ctor and args"

    let guid = Guid.NewGuid()
    let assemblyName = AssemblyName(guid.ToString())
    let moduleBuilder = Thread.GetDomain()
                              .DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run)
                              .DefineDynamicModule(assemblyName.Name)
    
    let typeBuilder = moduleBuilder.DefineType("GeneratedType", TypeAttributes.Class ||| TypeAttributes.Public)
    
    for expr in structAttrs do
        let (ctor, args) = ctorAndArgs expr
        typeBuilder.SetCustomAttribute(CustomAttributeBuilder(ctor, args))

    for (name, typ, attrExprs) in propsAndAttrs do
        // backing field
        let fieldBuilder = typeBuilder.DefineField("_" + name, typ, FieldAttributes.Private)

        // getter
        let getterBuilder = typeBuilder.DefineMethod("get_" + name, MethodAttributes.Public ||| MethodAttributes.SpecialName ||| MethodAttributes.HideBySig, typ, Type.EmptyTypes)
        let getterIL = getterBuilder.GetILGenerator()
        getterIL.Emit(OpCodes.Ldarg_0)
        getterIL.Emit(OpCodes.Ldfld, fieldBuilder)
        getterIL.Emit(OpCodes.Ret)
        
        // setter
        let setterBuilder = typeBuilder.DefineMethod("set_" + name, MethodAttributes.Public ||| MethodAttributes.SpecialName ||| MethodAttributes.HideBySig, null, [| typ |])
        let setterIL = setterBuilder.GetILGenerator()
        setterIL.Emit(OpCodes.Ldarg_0)
        setterIL.Emit(OpCodes.Ldarg_1)
        setterIL.Emit(OpCodes.Stfld, fieldBuilder)
        setterIL.Emit(OpCodes.Ret)

        // property
        let propBuilder = typeBuilder.DefineProperty(name, PropertyAttributes.None, typ, null)
        propBuilder.SetGetMethod(getterBuilder)
        propBuilder.SetSetMethod(setterBuilder)

        // attributes
        for expr in attrExprs do
            let (ctor, args) = ctorAndArgs expr
            let attrBuilder = CustomAttributeBuilder(ctor, args)
            propBuilder.SetCustomAttribute(attrBuilder)

    typeBuilder.CreateType()