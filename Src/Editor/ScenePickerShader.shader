// Copyright (c) 2024 Luke Shires
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

Shader "Hidden/ScenePickerShader"
{
    SubShader
    {
        Pass // front most depth values to z buffer
        {
			ZWrite On
			Cull Back
			ZTest LEqual

            CGPROGRAM
			#pragma vertex vert
            float4 vert(float4 v : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(v);
            }

            #pragma fragment frag
            void frag(float4 i : SV_POSITION)
            { }
            ENDCG
        }

		Pass // overlay to visible pixels
        {
			ZWrite Off
			Cull Back
			ZTest Equal
			Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            float4 vert(float4 v : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(v);
            }

            #pragma fragment frag
            fixed4 frag(float4 i : SV_POSITION) : SV_Target
            {
                return fixed4(0.10, 0.15, 0.75, 0.55);
            }
            ENDCG
        }

		Pass // overlay to hidden pixels
        {
			ZWrite Off
			Cull Back
			ZTest NotEqual
			Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            float4 vert(float4 v : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(v);
            }

            #pragma fragment frag
            fixed4 frag(float4 i : SV_POSITION) : SV_Target
            {
                return fixed4(0.10, 0.15, 0.75, 0.40);
            }
            ENDCG
        }
    }
}