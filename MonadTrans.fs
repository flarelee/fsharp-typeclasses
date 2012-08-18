﻿module Control.Monad.Trans

open Prelude
open Control.Monad.Base

let singleton x = [x]
let concat (x:List<List<'a>>) :List<'a> = List.concat x

module MaybeT =

    type MaybeT<'Ma> = MaybeT of 'Ma with
        static member inline (?<-) (_Functor:Fmap, MaybeT x :MaybeT<'ma>, _) = fun (f:'a->'b) -> MaybeT (fmap (Option.map f) x) :MaybeT<'mb>

    let inline runMaybeT   (MaybeT m) = m
    type MaybeT<'Ma> with
        static member inline (?<-) (_Monad:Return,            _:MaybeT<'ma>, _) = MaybeT << return' << Just :'a -> MaybeT<'ma>
        static member inline (?<-) (_Monad:Bind  , MaybeT x :MaybeT<'ma>, _:MaybeT<'mb>) = 
            fun (f: 'a -> MaybeT<'mb>) -> (MaybeT <| do' {
                let! maybe_value = x
                return! match maybe_value with
                        | Nothing    -> return' Nothing
                        | Just value -> runMaybeT <| f value}) :MaybeT<'mb>

        static member inline (?<-) (_MonadPlus:Mzero,         _:MaybeT<_>, _) = fun () -> MaybeT (return' Nothing)
        static member inline (?<-) (_MonadPlus:Mplus, MaybeT x, _) = fun (MaybeT y) -> MaybeT <| do' {
                let! maybe_value = x
                return! match maybe_value with
                        | Nothing    -> y
                        | Just value -> x}

    let inline mapMaybeT f (MaybeT m) = MaybeT (f m)


module ListT =

    type ListT<'Ma> = ListT of 'Ma with
        static member inline (?<-) (_Functor:Fmap,             ListT x:ListT<'ma>, _) = fun (f:'a->'b) -> ListT (fmap (List.map f) x):ListT<'mb>

    let inline runListT (ListT m) = m
    type ListT<'Ma> with
        static member inline (?<-) (_Monad:Return,                      _:ListT<'ma>, _) = ListT << return' << singleton :'a -> ListT<'ma>
        static member inline (?<-) (_Monad:Bind  , ListT x:ListT<'ma>,  _:ListT<'mb>) =
            fun (k: 'a -> ListT<'mb>) -> 
                (ListT (x >>= mapM(runListT << k) >>= (concat >> return'))) :ListT<'mb>

        static member inline (?<-) (_MonadPlus:Mzero,        _:ListT<_>, _) = fun () -> ListT (return' [])
        static member inline (?<-) (_MonadPlus:Mplus, ListT x, _) = fun (ListT y) -> ListT <| do' {
            let! a = x
            let! b = y
            return (a ++ b)}

    let inline mapListT f (ListT  m) = ListT (f m)

open MaybeT
open ListT

type Lift = Lift with
    static member inline (?<-) (_MonadTrans:Lift, _:MaybeT<'m_a>, _) = MaybeT << (liftM Just)      :'ma -> MaybeT<'m_a>
    static member inline (?<-) (_MonadTrans:Lift, _: ListT<'m_a>, _) = ListT  << (liftM singleton) :'ma ->  ListT<'m_a> 

let inline lift (x:'ma) = Inline.instance Lift x


type LiftIO = LiftIO with  
    static member inline (?<-) (_MonadIO:LiftIO, _:MaybeT<'U>, _) = fun (x :IO<'a>) -> lift (Inline.instance LiftIO x)
    static member inline (?<-) (_MonadIO:LiftIO, _:ListT< 'U>, _) = fun (x :IO<'a>) -> lift (Inline.instance LiftIO x)
    static member        (?<-) (_MonadIO:LiftIO, _:IO<'a>    , _) = fun (x :IO<'a>) -> x

let inline liftIO (x: IO<'a>) = Inline.instance LiftIO x


open Control.Monad.Cont

type CallCC = CallCC with
    static member (?<-) (_MonadCont:CallCC, _:MaybeT<Cont<'r,Maybe<'a>>>, _) = fun (f:((_ -> MaybeT<Cont<_,'b>>) -> _)) -> MaybeT(callCC <| fun c -> runMaybeT(f (MaybeT << c << Just)))     :MaybeT<Cont<'r,Maybe<'a>>>
    static member (?<-) (_MonadCont:CallCC, _:ListT<Cont<'r,List<'a>>>  , _) = fun (f:((_ -> ListT<Cont<_,'b>> ) -> _)) -> ListT (callCC <| fun c -> runListT (f (ListT  << c << singleton))) :ListT<Cont<'r, List<'a>>>    
    static member (?<-) (_MonadCont:CallCC, _:Cont<'r,'a>, _) = callCC : (('a -> Cont<'r,'b>) -> _) -> _

let inline callCC f = Inline.instance CallCC f


open Control.Monad.State

type Get = Get with
    static member inline (?<-) (_MonadState:Get, _:MaybeT<_> , _) = fun () -> lift get
    static member inline (?<-) (_MonadState:Get, _:ListT<_>  , _) = fun () -> lift get
    static member        (?<-) (_MonadState:Get, _:State<_,_>, _) = fun () ->      get

let inline get() = Inline.instance Get ()

type Put = Put with
    static member inline (?<-) (_MonadState:Put, _:MaybeT<_> , _) = lift << put
    static member inline (?<-) (_MonadState:Put, _:ListT<_>  , _) = lift << put
    static member        (?<-) (_MonadState:Put, _:State<_,_>, _) =         put

let inline put x = Inline.instance Put x


open Control.Monad.Reader

type Ask = Ask with
    static member (?<-) (_MonadReader:Ask, _:MaybeT<Reader<'a,Maybe<'a>>>, _) = fun () -> lift ask :MaybeT<Reader<'a,Maybe<'a>>>
    static member (?<-) (_MonadReader:Ask, _:ListT<Reader< 'a,List< 'a>>>, _) = fun () -> lift ask : ListT<Reader<'a, List<'a>>>
    static member (?<-) (_MonadReader:Ask, _:Reader<'r,'r>               , _) = fun () ->      ask :Reader<'r,'r>

let inline ask() = Inline.instance Ask ()

type Local = Local with
    static member inline (?<-) (_MonadReader:Local, MaybeT m, _:MaybeT<_>  ) = fun f -> MaybeT <| local f m
    static member inline (?<-) (_MonadReader:Local, ListT  m, _: ListT<_>  ) = fun f -> ListT  <| local f m
    static member        (?<-) (_MonadReader:Local,        m, _:Reader<_,_>) = fun f ->           local f m

let inline local f m = Inline.instance (Local, m) f


open Control.Monad.Writer

type Tell = Tell with
    static member inline (?<-) (_MonadWriter:Tell, _:MaybeT<_>  , _) = lift << tell
    static member        (?<-) (_MonadWriter:Tell, _:Writer<_,_>, _) =         tell

let inline tell x = Inline.instance Tell x

type Listen = Listen with
    static member inline (?<-) (_MonadWriter:Listen, m, _:MaybeT<_>  ) = fun () ->
        let liftMaybe (m,w) = Option.map (fun x -> (x,w)) m
        MaybeT (listen (runMaybeT m) >>= (return' << liftMaybe))
    static member        (?<-) (_MonadWriter:Listen, m, _:Writer<_,_>) = fun () -> listen m

let inline listen m = Inline.instance (Listen, m) ()

type Pass = Pass with
    static member inline (?<-) (_MonadWriter:Pass, m, _:MaybeT<_>  ) = fun () -> MaybeT (runMaybeT m >>= maybe (return' Nothing) (liftM Just << pass << return'))
    static member        (?<-) (_MonadWriter:Pass, m, _:Writer<_,_>) = fun () -> pass m

let inline pass m = Inline.instance (Pass, m) ()
