// Copyright (c) 2010-2014 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

//------------------------------------------------------------------------------
// <auto-generated>
//     Types declaration for SharpDX.Direct3D11 namespace.
//     This code was generated by a tool.
//     Date : 6/25/2016 10:38:05 PM
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;
using System.Security;
namespace SharpDX.Direct3D11 {

#pragma warning disable 419
#pragma warning disable 1587
#pragma warning disable 1574
    
    /// <summary>	
    /// <p>Describes an effect variable.</p>	
    /// </summary>	
    /// <remarks>	
    /// <p><see cref="SharpDX.Direct3D11.EffectVariableDescription"/> is used with <strong><see cref="SharpDX.Direct3D11.EffectVariable.GetDescription"/></strong>.</p>	
    /// </remarks>	
    /// <include file='.\..\Documentation\CodeComments.xml' path="/comments/comment[@id='D3DX11_EFFECT_VARIABLE_FLAGS']/*"/>	
    /// <msdn-id>ff476306</msdn-id>	
    /// <unmanaged>D3DX11_EFFECT_VARIABLE_FLAGS</unmanaged>	
    /// <unmanaged-short>D3DX11_EFFECT_VARIABLE_FLAGS</unmanaged-short>	
    [Flags]
    public enum EffectVariableFlags : int {	
        
        /// <summary>	
        /// <dd> <p>Name of this variable, annotation, or structure member.</p> </dd>	
        /// </summary>	
        /// <include file='.\..\Documentation\CodeComments.xml' path="/comments/comment[@id='D3DX11_EFFECT_VARIABLE_ANNOTATION']/*"/>	
        /// <msdn-id>ff476306</msdn-id>	
        /// <unmanaged>D3DX11_EFFECT_VARIABLE_ANNOTATION</unmanaged>	
        /// <unmanaged-short>D3DX11_EFFECT_VARIABLE_ANNOTATION</unmanaged-short>	
        Annotation = unchecked((int)2),			
        
        /// <summary>	
        /// <dd> <p>Semantic string of this variable or structure member (<c>null</c> for annotations or if not present).</p> </dd>	
        /// </summary>	
        /// <include file='.\..\Documentation\CodeComments.xml' path="/comments/comment[@id='D3DX11_EFFECT_VARIABLE_EXPLICIT_BIND_POINT']/*"/>	
        /// <msdn-id>ff476306</msdn-id>	
        /// <unmanaged>D3DX11_EFFECT_VARIABLE_EXPLICIT_BIND_POINT</unmanaged>	
        /// <unmanaged-short>D3DX11_EFFECT_VARIABLE_EXPLICIT_BIND_POINT</unmanaged-short>	
        ExplicitBindPoint = unchecked((int)4),			
        
        /// <summary>	
        /// None.	
        /// </summary>	
        /// <include file='.\..\Documentation\CodeComments.xml' path="/comments/comment[@id='']/*"/>	
        /// <unmanaged>None</unmanaged>	
        /// <unmanaged-short>None</unmanaged-short>	
        None = unchecked((int)0),			
    }
}