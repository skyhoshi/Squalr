/************************************************************************
   AvalonDock

   Copyright (C) 2007-2013 Xceed Software Inc.

   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/licenses/MS-PL
 ************************************************************************/

using System;

namespace Squalr.Theme.Themes
{
	/// <inheritdoc/>
	public class SqualrTheme : ThemeBase
	{
		/// <inheritdoc/>
		public override Uri GetResourceUri()
		{
			return new Uri("/Squalr.Theme;component/Themes/DarkTheme.xaml", UriKind.Relative);
		}
	}
}
