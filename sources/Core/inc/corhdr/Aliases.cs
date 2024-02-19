// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using unsafe mdScope = void*;            // Obsolete; not used in the runtime.
global using mdToken = uint;                    // Generic token

// Token definitions

global using mdModule = uint;                   // Module token (roughly, a scope)
global using mdTypeRef = uint;                  // TypeRef reference (this or other scope)
global using mdTypeDef = uint;                  // TypeDef in this scope
global using mdFieldDef = uint;                 // Field in this scope
global using mdMethodDef = uint;                // Method in this scope
global using mdParamDef = uint;                 // param token
global using mdInterfaceImpl = uint;            // interface implementation token

global using mdMemberRef = uint;                // MemberRef (this or other scope)
global using mdCustomAttribute = uint;          // attribute token
global using mdPermission = uint;               // DeclSecurity

global using mdSignature = uint;                // Signature object
global using mdEvent = uint;                    // event token
global using mdProperty = uint;                 // property token

global using mdModuleRef = uint;                // Module reference (for the imported modules)

// Assembly tokens.
global using mdAssembly = uint;                 // Assembly token.
global using mdAssemblyRef = uint;              // AssemblyRef token.
global using mdFile = uint;                     // File token.
global using mdExportedType = uint;             // ExportedType token.
global using mdManifestResource = uint;         // ManifestResource token.

global using mdTypeSpec = uint;                 // TypeSpec object

global using mdGenericParam = uint;             // formal parameter to generic type or method
global using mdMethodSpec = uint;               // instantiation of a generic method
global using mdGenericParamConstraint = uint;   // constraint on a formal generic parameter

// Application string.
global using mdString = uint;                   // User literal string token.

global using mdCPToken = uint;                  // constant pool token

global using RID = uint;

global using COR_SIGNATURE = byte;
global using unsafe PCOR_SIGNATURE = byte*;     // pointer to a cor sig. Not void* so that the bytes can be incremented easily
global using unsafe PCCOR_SIGNATURE = byte*;

global using unsafe MDUTF8CSTR = sbyte*;
global using unsafe MDUTF8STR = sbyte*;

//
// Opaque types for security properties and values.
//
global using unsafe PSECURITY_PROPS = void*;
global using unsafe PSECURITY_VALUE = void* ;
global using unsafe PPSECURITY_PROPS = void**;
global using unsafe PPSECURITY_VALUE = void**;
