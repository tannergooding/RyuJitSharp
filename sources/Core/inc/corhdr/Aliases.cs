// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using unsafe mdScope = void*;            // Obsolete; not used in the runtime.
global using mdToken = int;                     // Generic token

// Token definitions

global using mdModule = int;                    // Module token (roughly, a scope)
global using mdTypeRef = int;                   // TypeRef reference (this or other scope)
global using mdTypeDef = int;                   // TypeDef in this scope
global using mdFieldDef = int;                  // Field in this scope
global using mdMethodDef = int;                 // Method in this scope
global using mdParamDef = int;                  // param token
global using mdInterfaceImpl = int;             // interface implementation token

global using mdMemberRef = int;                 // MemberRef (this or other scope)
global using mdCustomAttribute = int;           // attribute token
global using mdPermission = int;                // DeclSecurity

global using mdSignature = int;                 // Signature object
global using mdEvent = int;                     // event token
global using mdProperty = int;                  // property token

global using mdModuleRef = int;                 // Module reference (for the imported modules)

// Assembly tokens.
global using mdAssembly = int;                  // Assembly token.
global using mdAssemblyRef = int;               // AssemblyRef token.
global using mdFile = int;                      // File token.
global using mdExportedType = int;              // ExportedType token.
global using mdManifestResource = int;          // ManifestResource token.

global using mdTypeSpec = int;                  // TypeSpec object

global using mdGenericParam = int;              // formal parameter to generic type or method
global using mdMethodSpec = int;                // instantiation of a generic method
global using mdGenericParamConstraint = int;    // constraint on a formal generic parameter

// Application string.
global using mdString = int;                    // User literal string token.

global using mdCPToken = int;                   // constant pool token

global using RID = int;

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
