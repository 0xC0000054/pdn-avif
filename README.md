# pdn-avif

A [Paint.NET](http://www.getpaint.net) filetype plugin that allows AVIF images to be loaded and saved with transparency.

Please note that this plugin is a beta version.
You should save your work in another format first, as it is possible that the plugin could hang or crash Paint.NET.

## Installation

1. Close Paint.NET.
2. Place AvifFileType.dll, AvifNative_x86.dll and AvifNative_x64.dll in the Paint.NET FileTypes folder which is usually located in one the following locations depending on the Paint.NET version you have installed.

  Paint.NET Version |  FileTypes Folder Location
  --------|----------
  Classic | C:\Program Files\Paint.NET\FileTypes    
  Microsoft Store | Documents\paint.net App Files\FileTypes

3. Open the Windows Run dialog (Start > Run or `Windows Key` + `R`)
4. Type `paintdotnet:/set:FileTypes/BuiltInAV1FileTypeEnabled=false` and press the `Enter` key
5. Restart Paint.NET.

## License

This project is licensed under the terms of the MIT License.   
See [License.txt](License.txt) for more information.

# Source code

## Prerequisites

* Visual Studio 2019
* Paint.NET 4.2.10 or later

## Building the plugin

* Open the solution
* Change the PaintDotNet references in the AvifFileType project to match your Paint.NET install location
* Update the post build events to copy the build output to the Paint.NET FileTypes folder
* Build the solution

## 3rd Party Code

This project uses the following libraries. (the required header and library files are located in the `src/deps/` sub-folders).

* [aom](https://aomedia.googlesource.com/aom/) (v1.0.0-errata1-avif tag)