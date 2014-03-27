﻿namespace MathNet.Symbolics

open System
open System.Numerics
open MathNet.Numerics
open MathNet.Symbolics

/// Single-Variable Polynomial (2*x+3*x^2)
module Polynomials =

    open Numbers

    let rec isMonomial symbol = function
        | x when x = symbol -> true
        | Number _ -> true
        | Power (r, (Number (Integer n) as p)) when r = symbol && n > BigInteger.One -> true
        | Product ax -> List.forall (isMonomial symbol) ax
        | _ -> false

    let isPolynomial symbol = function
        | Sum ax -> List.forall (isMonomial symbol) ax
        | x when isMonomial symbol x -> true
        | _ -> false

    let rec degreeMonomial symbol = function
        | x when x = zero -> -infinity
        | x when x = symbol -> one
        | Number _ -> zero
        | Power (r, (Number (Integer n) as p)) when r = symbol && n > BigInteger.One -> p
        | Product ax -> sum <| List.map (degreeMonomial symbol) ax
        | _ -> undefined

    let degree symbol x =
        let d = degreeMonomial symbol x
        if d <> undefined then d else
        match x with
        | Sum ax -> max <| List.map (degreeMonomial symbol) ax
        | _ -> undefined

    let rec coefficientMonomial symbol = function
        | x when x = symbol -> one
        | Number _ as x -> x
        | Power (r, (Number (Integer n) as p)) when r = symbol && n > BigInteger.One -> one
        | Product ax -> product <| List.map (coefficientMonomial symbol) ax
        | _ -> undefined

    let rec coefficientDegreeMonomial symbol = function
        | x when x = zero -> x, -infinity
        | x when x = symbol -> one, one
        | Number _ as x -> x, zero
        | Power (r, (Number (Integer n) as p)) when r = symbol && n > BigInteger.One -> one, p
        | Product ax ->
            let cds = List.map (coefficientDegreeMonomial symbol) ax
            product <| List.map fst cds, sum <| List.map snd cds
        | _ -> undefined, undefined

    let coefficient symbol (k:int) x =
        let ke = number k
        let c, d = coefficientDegreeMonomial symbol x
        if d = ke then c else
        match x with
        | Sum ax -> List.map (coefficientDegreeMonomial symbol) ax |> List.filter (fun (_, d) -> d = ke) |> List.map fst |> sum
        | _ -> undefined

    let leadingCoefficientDegree symbol x =
        let c, d = coefficientDegreeMonomial symbol x
        if d <> undefined then c, d else
        match x with
        | Sum ax ->
            let cds = List.map (coefficientDegreeMonomial symbol) ax
            let degree = max <| List.map snd cds
            cds |> List.filter (fun (_, d) -> d = degree) |> List.map fst |> sum, degree
        | _ -> undefined, undefined

    let leadingCoefficient symbol x = leadingCoefficientDegree symbol x |> fst

    let coefficients symbol x =
        let rec collect symbol = function
            | x when x = symbol -> [1, one]
            | Number _ as a -> [0, a]
            | Power (r, (Number (Integer n) as p)) when r = symbol && n > BigInteger.One -> [int n, one]
            | Sum ax -> List.collect (collect symbol) ax
            | Product ax -> List.map (collect symbol) ax |> List.reduce (fun a b -> a |> List.fold (fun s (o1, e1) -> b |> List.fold (fun s (o2, e2) -> (o1+o2,e1*e2)::s) s) [])
            | _ -> []
        let c = collect symbol x
        let degree = c |> Seq.map fst |> Seq.max
        c |> List.fold (fun (s:Expression[]) (o,e) -> s.[o] <- s.[o] + e; s) (Array.create (degree+1) zero)


/// General Polynomial Expressions
module GeneralPolynomials =

    open System.Collections.Generic
    open Numbers
    open Elementary

    let variables x =
        let rec impl symbols = function
            | Number _ -> symbols
            | Power (r, (Number (Integer n) as p)) when n > BigInteger.One -> Set.add r symbols
            | Power _ as p -> Set.add p symbols
            | Sum ax -> ax |> List.fold impl symbols
            | Product ax -> ax |> List.fold (fun s a -> match a with | Sum _ as z -> Set.add z s | _ -> impl s a) symbols
            | _ as z -> Set.add z symbols
        impl Set.empty x

    let rec isMonomial (symbols: Set<Expression>) = function
        | x when symbols.Contains(x) -> true
        | Number _ -> true
        | Power (r, (Number (Integer n) as p)) when symbols.Contains(r) && n > BigInteger.One -> true
        | Product ax -> List.forall (isMonomial symbols) ax
        | x -> freeOfSet symbols x

    let isPolynomial (symbols: Set<Expression>) = function
        | Sum ax -> List.forall (isMonomial symbols) ax
        | x when isMonomial symbols x -> true
        | _ -> false

    let rec degreeMonomial (symbols: Set<Expression>) = function
        | x when x = zero -> -infinity
        | x when symbols.Contains(x) -> one
        | Number _ -> zero
        | Power (r, (Number (Integer n) as p)) when symbols.Contains(r) && n > BigInteger.One -> p
        | Product ax -> sum <| List.map (degreeMonomial symbols) ax
        | x when freeOfSet symbols x -> zero
        | _ -> undefined

    let degree (symbols: Set<Expression>) x =
        let d = degreeMonomial symbols x
        if d <> undefined then d else
        match x with
        | Sum ax -> max <| List.map (degreeMonomial symbols) ax
        | _ -> undefined

    let totalDegree x = degree (variables x) x

    let rec coefficientDegreeMonomial symbol = function
        | x when x = symbol -> one, one
        | Number _ as x -> x, zero
        | Power (r, (Number (Integer n) as p)) when r = symbol && n > BigInteger.One -> one, p
        | Product ax ->
            let cds = List.map (coefficientDegreeMonomial symbol) ax
            product <| List.map fst cds, sum <| List.map snd cds
        | x when freeOf symbol x -> x, zero
        | _ -> undefined, undefined

    let coefficient symbol (k:int) x =
        let ke = number k
        let c, d = coefficientDegreeMonomial symbol x
        if d = ke then c else
        match x with
        | Sum ax -> List.map (coefficientDegreeMonomial symbol) ax |> List.filter (fun (_, d) -> d = ke) |> List.map fst |> sum
        | _ -> undefined

    let leadingCoefficientDegree symbol x =
        let c, d = coefficientDegreeMonomial symbol x
        if d <> undefined then c, d else
        match x with
        | Sum ax ->
            let cds = List.map (coefficientDegreeMonomial symbol) ax
            let degree = max <| List.map snd cds
            cds |> List.filter (fun (_, d) -> d = degree) |> List.map fst |> sum, degree
        | _ -> undefined, undefined

    let leadingCoefficient symbol x = leadingCoefficientDegree symbol x |> fst

    let coefficients symbol x =
        let rec collect symbol = function
            | x when x = symbol -> [1, one]
            | Number _ as a -> [0, a]
            | Power (r, (Number (Integer n) as p)) when r = symbol && n > BigInteger.One -> [int n, one]
            | Sum ax -> List.collect (collect symbol) ax
            | Product ax -> List.map (collect symbol) ax |> List.reduce (fun a b -> a |> List.fold (fun s (o1, e1) -> b |> List.fold (fun s (o2, e2) -> (o1+o2,e1*e2)::s) s) [])
            | x when freeOf symbol x -> [0, x]
            | _ -> []
        let c = collect symbol x
        let degree = c |> Seq.map fst |> Seq.max
        c |> List.fold (fun (s:Expression[]) (o,e) -> s.[o] <- s.[o] + e; s) (Array.create (degree+1) zero)

    let rec collectTermsMonomial (symbols: Set<Expression>) = function
        | x when symbols.Contains(x) -> (one, x)
        | Number _ as x-> (x, one)
        | Power (r, (Number (Integer n) as p)) as x when symbols.Contains(r) && n > BigInteger.One -> (one, x)
        | Product ax -> List.map (collectTermsMonomial symbols) ax |> List.reduce (fun (c1, v1) (c2, v2) -> (c1*c2, v1*v2))
        | x when freeOfSet symbols x -> (x, one)
        | _ -> (undefined, undefined)

    let collectTerms (symbols: Set<Expression>) = function
        | Sum ax -> List.map (collectTermsMonomial symbols) ax |> Seq.groupBy snd |> Seq.map (fun (v, cs) -> (Seq.map fst cs |> sumSeq) * v) |> sumSeq
        | x -> let c, v = collectTermsMonomial symbols x in if c <> undefined then c*v else undefined