﻿namespace MathNet.Symbolics

open System
open System.Numerics
open MathNet.Numerics
open MathNet.Symbolics

type Expression =
    | Number of Number
    | Identifier of Symbol
    | Sum of Expression list
    | Product of Expression list
    | Power of Expression * Expression
    | Function of Function * Expression
    | FunctionN of Function * (Expression list)

    static member Zero = Number (Number.Zero)
    static member One = Number (Number.One)
    static member Two = Number (Number.OfInt32(2))
    static member MinusOne = Number (Number.MinusOne)
    static member Undefined = Identifier Undefined
    static member Infinity = Identifier Infinity
    static member OfInt32 (x:int) = Number (Number.OfInt32(x))
    static member OfInteger (x:BigInteger) = Number (Integer x)
    static member OfRational (x:BigRational) = Number (Number.Reduce(x))

    static member ( ~+ ) (x:Expression) = x
    static member ( ~- ) (x:Expression) = Expression.MinusOne * x
    static member ( - ) ((x:Expression), (y:Expression)) = x + (-y)
    static member ( / ) (x, y) = x * Expression.Invert y

    static member ( + ) (x, y) =
        // none of the summands is allowed to be a sum
        // only the first summand is allowed to be a number

        let (|Term|_|) = function
            | Number _ -> None
            | Product [(Number a); b] -> Some (a, b)
            | Product ((Number a)::xs) -> Some (a, Product xs)
            | x -> Some (Number.One, x)

        // xs and ys must *not* contain a (first) constant term
        let merge (xs:Expression list) (ys:Expression list) =
            let rec gen acc u v =
                match acc, u, v with
                | Term(ac,at)::cc, Term(xc,xt)::xs, y | Term(ac,at)::cc, y, Term(xc,xt)::xs when at = xt ->
                    gen ((Number(ac+xc)*at)::cc) xs y
                | _, Term(xc,xt)::xs, Term(yc,yt)::ys when xt = yt ->
                    gen ((Number(xc+yc)*xt)::acc) xs ys
                | _, (Term(xc,Identifier(Symbol xt)) as x)::xs, (Term(yc,Identifier(Symbol yt)) as y)::ys ->
                    if xt <= yt then gen (x::acc) xs v
                    else gen (y::acc) u ys
                | _, x::xs, y -> gen (x::acc) xs y
                | _, [], y::ys -> gen (y::acc) ys []
                | _, [], [] -> acc
            match gen [] xs ys with
            | [x] -> x
            | [] -> Expression.Zero
            | x -> Sum (List.rev x)

        match x, y with
        | a, b | b, a when a = Expression.Zero -> b
        | a, b | b, a when a = Expression.Undefined -> Expression.Undefined
        | Sum ((Number a)::ax), Sum ((Number b)::bx) -> (merge ax bx) + (Number (a+b))
        | Sum ((Number a)::ax), Sum bx | Sum bx, Sum ((Number a)::ax) -> (merge ax bx) + (Number a)
        | Sum ((Number a)::ax), Number b | Number b, Sum ((Number a)::ax) -> Sum (Number (a+b)::ax)
        | Sum ((Number a)::ax), b | b, Sum ((Number a)::ax) -> (merge ax [b]) + (Number a)
        | Sum ax, Sum bx -> merge ax bx
        | Sum ax, Number b | Number b, Sum ax -> Sum ((Number b)::ax)
        | Sum ax, b -> merge ax [b]
        | a, Sum bx -> merge [a] bx
        | Number a, Number b -> Number (a+b)
        | Number a, b | b, Number a -> Sum [Number a; b]
        | a, b -> merge [a] [b]

    static member ( * ) (x, y) =
        // none of the factors is allowed to be a product
        // only the first factor is allowed to be a number

        let (|Term|_|) = function
            | Number _ -> None
            | Power (r,p) -> Some (r, p)
            | x -> Some (x, Expression.One)

        // xs and ys must *not* contain a (first) constant term
        let merge (xs:Expression list) (ys:Expression list) =
            let rec gen acc u v =
                match acc, u, v with
                | Term(ab,ae)::cc, Term(xb,xe)::xs, y | Term(ab,ae)::cc, y, Term(xb,xe)::xs when ab = xb ->
                    gen ((ab**(ae+xe))::cc) xs y
                | _, Term(xb,xe)::xs, Term(yb,ye)::ys when xb = yb ->
                    gen ((xb**(xe+ye))::acc) xs ys
                | _, (Term(Identifier(Symbol xb),xe) as x)::xs, (Term(Identifier(Symbol yb),ye) as y)::ys ->
                    if xb <= yb then gen (x::acc) xs v
                    else gen (y::acc) u ys
                | _, x::xs, y -> gen (x::acc) xs y
                | _, [], y::ys -> gen (y::acc) ys []
                | _, [], [] -> acc
            match gen [] xs ys with
            | [x] -> x
            | [] -> Expression.One
            | x -> Product (List.rev x)

        match x, y with
        | a, b | b, a when a = Expression.One -> b
        | a, b | b, a when a = Expression.Zero -> Expression.Zero
        | a, b | b, a when a = Expression.Undefined -> Expression.Undefined
        | Product ((Number a)::ax), Product ((Number b)::bx) -> (merge ax bx) * (Number (a*b))
        | Product ((Number a)::ax), Product bx | Product bx, Product ((Number a)::ax) -> (merge ax bx) * (Number a)
        | Product ((Number a)::ax), Number b | Number b, Product ((Number a)::ax) -> Product (Number (a*b)::ax)
        | Product ((Number a)::ax), b | b, Product ((Number a)::ax) -> (merge ax [b]) * (Number a)
        | Product ax, Product bx -> merge ax bx
        | Product ax, Number b | Number b, Product ax -> Product ((Number b)::ax)
        | Product ax, b -> merge ax [b]
        | a, Product bx -> merge [a] bx
        | Number a, Number b -> Number (a*b)
        | Number a, b | b, Number a -> Product [Number a; b]
        | a, b -> merge [a] [b]

    static member Pow (x, y) =
        // if power is a number, radix must not be an integer, fraction, product or power
        match x, y with
        | a, b when b = Expression.Zero && a = Expression.Zero -> Expression.Undefined
        | a, b when b = Expression.Zero -> Expression.One
        | a, b when b = Expression.One -> a
        | a, b when a = Expression.One -> Expression.One
        | a, b | b, a when a = Expression.Undefined -> Expression.Undefined
        | Number a, Number (Integer b) -> Number (a ** int b)
        | Product ax, Number (Integer b) -> Product (ax |> List.map (fun z -> Expression.Pow(z,y)))
        | Power (r, p), Number (Integer b) -> Power (r, p*y)
        | a, b -> Power(a, b)

    static member Invert (x) =
        match x with
        | a when a = Expression.Undefined -> Expression.Undefined
        | a when a = Expression.Infinity -> Expression.Zero
        | a when a = Expression.Zero -> Expression.Undefined // no direction
        | Number a -> Number (Number.Invert a)
        | Product ax -> Product (ax |> List.map (Expression.Invert))
        | Power (r, p) -> Power (r, -p)
        | x -> Power (x, Expression.MinusOne)

    // Simpler usage
    static member ( + ) (x, (y:int)) = x + Number (Integer (BigInteger(y)))
    static member ( + ) ((x:int), y) = Number (Integer (BigInteger(x))) + y
    static member ( - ) (x, (y:int)) = x - Number (Integer (BigInteger(y)))
    static member ( - ) ((x:int), y) = Number (Integer (BigInteger(x))) - y
    static member ( * ) (x, (y:int)) = x * Number (Integer (BigInteger(y)))
    static member ( * ) ((x:int), y) = Number (Integer (BigInteger(x))) * y
    static member ( / ) (x, (y:int)) = x / Number (Integer (BigInteger(y)))
    static member ( / ) ((x:int), y) = Number (Integer (BigInteger(x))) / y
    static member Pow (x, (y:int)) = Expression.Pow(x, Number (Integer (BigInteger(y))))