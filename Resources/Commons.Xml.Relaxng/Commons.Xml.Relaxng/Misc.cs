//
// Commons.Xml.Relaxng.General.cs
//
// Author:
//	Atsushi Enomoto <ginga@kit.hi-ho.ne.jp>
//
// 2003 Atsushi Enomoto "No rights reserved."
//
// Copyright (c) 2004 Novell Inc.
// All rights reserved
//

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
//
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Xml;
using Commons.Xml.Relaxng.Derivative;

namespace Commons.Xml.Relaxng
{
	internal class Util
	{
		public static string NormalizeWhitespace (string s)
		{
			if (s.Length == 0)
				return s;

			StringBuilder sb = null;
			int noSpaceIndex = 0;
			bool inSpace = false;

			for (int i = 0; i < s.Length; i++) {
				switch (s [i]) {
				case ' ':
				case '\r':
				case '\t':
				case '\n':
					if (inSpace)
						continue;
					if (sb == null)
						sb = new StringBuilder (s.Length);
					if (noSpaceIndex < i) {
						if (sb.Length > 0)
							sb.Append (' ');
						sb.Append (s, noSpaceIndex, i - noSpaceIndex);
					}
					inSpace = true;
					break;
				default:
					if (inSpace) {
						noSpaceIndex = i;
						inSpace = false;
					}
					break;
				}
			}
			if (sb == null)
				return s;
			if (!inSpace && noSpaceIndex < s.Length) {
				sb.Append (' ');
				sb.Append (s, noSpaceIndex, s.Length - noSpaceIndex);
			}
			return sb.ToString ();
		}

		public static bool IsWhitespace (string s)
		{
			for (int i = 0; i < s.Length; i++) {
				switch (s [i]) {
				case ' ':
				case '\t':
				case '\n':
				case '\r':
					continue;
				default:
					return false;
				}
			}
			return true;
		}
	}
}

