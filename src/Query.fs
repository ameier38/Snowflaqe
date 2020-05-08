﻿module Snowflake.Query

open Snowflake.Types
open GraphQLParser.AST
open GraphQLParser

let rec readNode (node: ASTNode) =
    match node.Kind with
    | ASTNodeKind.Name ->
        let nameNode = unbox<GraphQLName> node
        Some (GraphqlNode.Name(nameNode, nameNode.Location))

    | ASTNodeKind.SelectionSet ->
        let selectionSet = unbox<GraphQLSelectionSet> node
        Some (GraphqlNode.SelectionSet (readSelections selectionSet))

    | ASTNodeKind.Field ->
        let field = unbox<GraphQLFieldSelection> node
        let fieldSelection : GraphqlFieldSelection = {
            name = field.Name.Value
            arguments = listOrNone field.Arguments
            selectionSet = if isNull field.SelectionSet then None else Some (readSelections field.SelectionSet)
            directives = listOrNone field.Directives
            location = field.Location
        }

        Some (GraphqlNode.Field fieldSelection)

    | ASTNodeKind.FragmentSpread -> 
        let fragmentSpread = unbox<GraphQLFragmentSpread> node 
        Some (GraphqlNode.FragmentSpread fragmentSpread)

    | ASTNodeKind.FragmentDefinition ->     
        let fragmentDef = unbox<GraphQLFragmentDefinition> node 
        let def : GraphqlFragmentDefinition = {
            name = fragmentDef.Name.Value 
            selectionSet = if isNull fragmentDef.SelectionSet then None else Some (readSelections fragmentDef.SelectionSet)
            typeDef = fragmentDef.TypeCondition.Name.Value
            directives = listOrNone fragmentDef.Directives
            location = fragmentDef.Location
        }

        Some (GraphqlNode.FragmentDefinition def)

    | ASTNodeKind.OperationDefinition ->
        let operation = unbox<GraphQLOperationDefinition> node
        match operation.Operation with
        | OperationType.Query ->
            let name =
                if isNull operation.Name
                then None
                else Option.ofObj operation.Name.Value

            let query = GraphqlNode.Query {
                name = name
                directives = listOrNone operation.Directives
                variables = listOrNone operation.VariableDefinitions
                selectionSet = readSelections operation.SelectionSet
            }

            Some query

        | OperationType.Mutation ->
            let name =
                if isNull operation.Name
                then None
                else Option.ofObj operation.Name.Value

            let mutation = GraphqlNode.Mutation {
                name = name
                directives = listOrNone operation.Directives
                variables = listOrNone operation.VariableDefinitions
                selectionSet = readSelections operation.SelectionSet
            }

            Some mutation

        | _ -> 
            None
    | _ ->
        None

and readSelections (selectionSet: GraphQLSelectionSet) : SelectionSet = {
    location = selectionSet.Location
    nodes = List.choose readNode (List.ofSeq selectionSet.Selections)
}

let private lexer = Lexer()
let private parser = Parser(lexer)

let parse (content: string) : Result<GraphqlDocument, string> =
    try 
        let ast = parser.Parse(Source content)
        Ok { nodes = List.choose readNode (List.ofSeq ast.Definitions) }
    with 
    | ex -> Error ex.Message

let findTypeByName (name: string) (schema: GraphqlSchema) =
    schema.types
    |> List.tryFind (function
        | GraphqlType.Enum enumDef -> enumDef.name = name
        | GraphqlType.Object objectDef -> objectDef.name = name
        | GraphqlType.Scalar scalar -> false)


let validate (document: GraphqlDocument) (schema: GraphqlSchema) : bool = false