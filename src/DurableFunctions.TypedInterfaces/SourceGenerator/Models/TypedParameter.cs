// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Models
{
    public class TypedParameter
    {
        #region Properties

        public TypeSyntax Type { get; }
        public string Name { get; }

        #endregion

        #region Constructors

        public TypedParameter(TypeSyntax type, string name)
        {
            Type = type;
            Name = name;
        }

        #endregion

        #region Public Methods

        public override string ToString()
        {
            return $"{Type} {Name}";
        }

        #endregion
    }
}
