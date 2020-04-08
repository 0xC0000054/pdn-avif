# pdn-avif

A [Paint.NET](http://www.getpaint.net) filetype plugin that allows AVIF images to be loaded and saved with transparency.

## Installation

The plugin currently requires that Paint.NET's built-in AVIF support be disabled via a hidden registry setting before it can be used.
Because of this binaries are not provided.

## Known issues

AV1 files that use image grids are not supported.   
For example, [Summer_in_Tomsk_720p_5x4_grid.avif](https://github.com/AOMediaCodec/av1-avif/blob/master/testFiles/Microsoft/Summer_in_Tomsk_720p_5x4_grid.avif).

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
* [Little CMS](https://github.com/mm2/Little-CMS)  (version 2.9)