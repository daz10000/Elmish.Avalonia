﻿namespace Elmish.Avalonia

open Elmish
open Avalonia.Threading
open Avalonia.Controls
open System.ComponentModel
open System.Reactive.Subjects
open System.Reactive.Linq
open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type ReactiveElmishViewModel() = 
    inherit ReactiveUI.ReactiveObject()

    let disposables = ResizeArray<IDisposable>()
    let propertyChanged = Event<_, _>()
    let propertySubscriptions = Dictionary<string, IDisposable>()

    member val Root: ICompositionRoot = Unchecked.defaultof<_> with get, set

    member this.GetView<'ViewModel & #ReactiveUI.IReactiveObject>() = 
        let vmType = typeof<'ViewModel>
        this.Root.GetView(vmType)
    
    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member this.PropertyChanged = propertyChanged.Publish

    /// Fires the `PropertyChanged` event for the given property name. Uses the caller's name if no property name is given.
    member this.OnPropertyChanged([<CallerMemberName; Optional; DefaultParameterValue("")>] ?propertyName: string) =
        propertyChanged.Trigger(this, PropertyChangedEventArgs(propertyName.Value))

    /// Binds a VM property to a 'Model projection and refreshes the VM property when the 'Model projection changes.
    member this.Bind<'Model, 'Msg, 'ModelProjection>(store: IElmishStore<'Model, 'Msg>, modelProjection: 'Model -> 'ModelProjection, [<CallerMemberName; Optional; DefaultParameterValue("")>] ?vmPropertyName) = 
        let vmPropertyName = vmPropertyName.Value
        if not (propertySubscriptions.ContainsKey vmPropertyName) then
            // Creates a subscription to the 'Model projection and stores it in a dictionary.
            let disposable = 
                store.ModelObservable
                    .DistinctUntilChanged(modelProjection)
                    .Subscribe(fun _ -> 
                        // Alerts the view that the 'Model projection / VM property has changed.
                        this.OnPropertyChanged(vmPropertyName)
                        #if DEBUG
                        printfn $"PropertyChanged: {vmPropertyName} by {this}"
                        #endif
                    )

            // Stores the subscription and the boxed model projection ('Model -> obj) in a dictionary
            propertySubscriptions.Add(vmPropertyName, disposable)

        // Returns the latest value from the model projection.
        store.Model |> modelProjection

    member this.AddDisposable(disposable: 'Disposable & #IDisposable) =
        disposables.Add(disposable)

    interface IDisposable with
        member this.Dispose() =
            disposables |> Seq.iter _.Dispose()
            propertySubscriptions.Values |> Seq.iter _.Dispose()
            propertySubscriptions.Clear()



module ReactiveElmishViewModel = 
    let trySetRoot (root: ICompositionRoot) (vm: ReactiveUI.IReactiveObject) =
        if vm :? ReactiveElmishViewModel then
            (vm :?> ReactiveElmishViewModel).Root <- root
