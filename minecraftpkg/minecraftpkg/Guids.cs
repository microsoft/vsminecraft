// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

// Guids.cs
// MUST match guids.h
using System;

namespace Microsoft.minecraftpkg
{
    static class GuidList
    {
        public const string guidminecraftpkgPkgString = "baf7c18c-9b42-43bc-9859-f6d3803e27c0";
        public const string guidminecraftpkgCmdSetString = "bb6a5b13-7be9-4ba9-b42f-656e90d7e4f2";

        public static readonly Guid guidminecraftpkgCmdSet = new Guid(guidminecraftpkgCmdSetString);
    };
}