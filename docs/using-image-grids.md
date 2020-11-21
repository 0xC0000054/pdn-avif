## Using image grids to reduce memory usage when encoding

* TOC
{:toc}
### Introduction

As of version 2.0.0 the AOM encoder is very memory hungry, when saving a 4032x3024 pixel image without using an image grid I noticed in task manager that it was using approximately 2.5 GB of memory on speed 6 with Realtime usage.
The AOM encoder also lacks the ability to cancel an encode operation that is in progress. This is a problem for Paint.NET and other GUI applications that provide a preview to the user when saving,
as the user has to wait until AOM has finished encoding the current frame before the encode can restart with the new settings. 

To solve these problems the Paint.NET AvifFileType plugin uses an image grid when saving files at most compression speeds, this reduces the encoder memory usage and improves the UI responsiveness by providing more opportunities to report progress and cancel the encode operation after each frame is encoded.
An image grid is a collection of smaller images that a decoder will combine to form a larger image.
Many smart-phones that output images in HEIF-based formats (e.g. HEIC) use this technique.
A file saved using an image grid will be larger and take longer to encode than a file saved as a single image.
This is due to the fact that the encoder has to process multiple frames, and cannot compress the image data as efficiently as it can when encoding as a single frame.
There is also some additional meta-data overhead.

### Image grid layout

The following is a basic overview of the image grid format, please refer to the HEIF[^1] and MIAF[^2] specifications for more details on the image grid requirements.

An image that uses an image grid has the item information box type of 'grid' and an associated item reference with the 'dimg' type.
Images that use an image grid can have between 1 and 256 horizontal and vertical images.
While you may assume that an image grid can have up to 65536 items this is not the case, the ISOBMFF[^3] standard that HEIF is based on only allows an item reference to contain up to 65535 items.

When the decoder loads the image it will sequentially read each referenced image in the 'dimg' entry and copy the decoded image to the output image ordered from top to bottom and then from left to right.

[Image grid example.avif](images/Image grid example.avif) is a 512x512 pixel AVIF image that is comprised of a 2x2 grid of 256x256 pixel tiles. Item 5 is the grid image, items 1-4 are the image grid tiles. The following text is a portion of the output from the `heif-info --dump-boxes` command.

`reference with type 'dimg' from ID: 5 to IDs: 1 2 3 4`

<details><summary>Image grid example.avif box list</summary>
<p>
Box: ftyp -----<br>
size: 28 (header size: 8)<br>
major brand: avif<br>
minor version: 0<br>
compatible brands: avif,mif1,miaf<br>
<br>
Box: meta -----<br>
size: 557 (header size: 12)<br>
version: 0<br>flags: 0<br>
| Box: hdlr -----<br>
| size: 40 (header size: 12)<br>
| version: 0<br>
| flags: 0<br>
| pre_defined: 0<br>
| handler_type: pict<br>
| name: PDNavif<br>
| <br>
| Box: pitm -----<br>
| size: 14 (header size: 12)<br>
| version: 0<br>
| flags: 0<br>
| item_ID: 5<br>
| <br>
| Box: iloc -----<br>
| size: 112 (header size: 12)<br>
| version: 1<br>| flags: 0<br>
| item ID: 1<br>
|&nbsp;&nbsp; construction method: 0<br>
|&nbsp;&nbsp; data_reference_index: 0<br>
|&nbsp;&nbsp; base_offset: 0<br>
|&nbsp;&nbsp; extents: 593,953 <br>
| item ID: 2<br>
|&nbsp;&nbsp; construction method: 0<br>
|&nbsp;&nbsp; data_reference_index: 0<br>
|&nbsp;&nbsp; base_offset: 0<br>
|&nbsp;&nbsp; extents: 1546,1536 <br>
| item ID: 3<br>
|&nbsp;&nbsp; construction method: 0<br>
|&nbsp;&nbsp; data_reference_index: 0<br>
|&nbsp;&nbsp; base_offset: 0<br>
|&nbsp;&nbsp; extents: 3082,1913 <br>
| item ID: 4<br>
|&nbsp;&nbsp; construction method: 0<br>
|&nbsp;&nbsp; data_reference_index: 0<br>
|&nbsp;&nbsp; base_offset: 0<br>
|&nbsp;&nbsp; extents: 4995,800 <br>
| item ID: 5<br>
|&nbsp;&nbsp; construction method: 1<br>
|&nbsp;&nbsp; data_reference_index: 0<br>
|&nbsp;&nbsp; base_offset: 0<br>
|&nbsp;&nbsp; extents: 0,8 <br>
| item ID: 6<br>
|&nbsp;&nbsp; construction method: 0<br>
|&nbsp;&nbsp; data_reference_index: 0<br>
|&nbsp;&nbsp; base_offset: 0<br>
|&nbsp;&nbsp; extents: 5795,178 <br>
| <br>
| Box: iinf -----<br>
| size: 149 (header size: 12)<br>
| version: 0<br>| flags: 0<br>
| | Box: infe -----<br>
| | size: 21 (header size: 12)<br>
| | version: 2<br>
| | flags: 0<br>
| | item_ID: 1<br>
| | item_protection_index: 0<br>
| | item_type: av01<br>
| | item_name: <br>
| | content_type: <br>
| | content_encoding: <br>
| | item uri type: <br>
| | hidden item: false<br>
| | <br>
| | Box: infe -----<br>
| | size: 21 (header size: 12)<br>
| | version: 2<br>
| | flags: 0<br>
| | item_ID: 2<br>
| | item_protection_index: 0<br>
| | item_type: av01<br>
| | item_name: <br>
| | content_type: <br>
| | content_encoding: <br>
| | item uri type: <br>
| | hidden item: false<br>
| | <br>
| | Box: infe -----<br>
| | size: 21 (header size: 12)<br>
| | version: 2<br>
| | flags: 0<br>
| | item_ID: 3<br>
| | item_protection_index: 0<br>
| | item_type: av01<br>
| | item_name: <br>
| | content_type: <br>
| | content_encoding: <br>
| | item uri type: <br>
| | hidden item: false<br>
| | <br>
| | Box: infe -----<br>
| | size: 21 (header size: 12)<br>
| | version: 2<br>
| | flags: 0<br>
| | item_ID: 4<br>
| | item_protection_index: 0<br>
| | item_type: av01<br>
| | item_name: <br>
| | content_type: <br>
| | content_encoding: <br>
| | item uri type: <br>
| | hidden item: false<br>
| | <br>
| | Box: infe -----<br>
| | size: 26 (header size: 12)<br>
| | version: 2<br>
| | flags: 0<br>
| | item_ID: 5<br>
| | item_protection_index: 0<br>
| | item_type: grid<br>
| | item_name: Color<br>
| | content_type: <br>
| | content_encoding: <br>
| | item uri type: <br>
| | hidden item: false<br>
| | <br>
| | Box: infe -----<br>
| | size: 25 (header size: 12)<br>
| | version: 2<br>
| | flags: 0<br>
| | item_ID: 6<br>
| | item_protection_index: 0<br>
| | item_type: Exif<br>
| | item_name: Exif<br>
| | content_type: <br>
| | content_encoding: <br>
| | item uri type: <br>
| | hidden item: false<br>
| <br>
| Box: iref -----<br>
| size: 46 (header size: 12)<br>
| version: 0<br>
| flags: 0<br>
| reference with type 'dimg' from ID: 5 to IDs: 1 2 3 4 <br>| reference with type 'cdsc' from ID: 6 to IDs: 5 <br>
| <br>
| Box: iprp -----<br>
| size: 168 (header size: 8)<br>
| | Box: ipco -----<br>
| | size: 111 (header size: 8)<br>
| | | Box: ispe -----<br>
| | | size: 20 (header size: 12)<br>
| | | version: 0<br>
| | | flags: 0<br>
| | | image width: 256<br>
| | | image height: 256<br>
| | | <br>
| | | Box: pasp -----<br>
| | | size: 16 (header size: 8)<br>
| | | <br>
| | | Box: av1C -----<br>
| | | size: 12 (header size: 8)<br>
| | | version: 1<br>
| | | seq_profile: 0<br>
| | | seq_level_idx_0: 0<br>
| | | high_bitdepth: 0<br>
| | | twelve_bit: 0<br>
| | | chroma_subsampling_x: 1<br>
| | | chroma_subsampling_y: 1<br>
| | | chroma_sample_position: 0<br>
| | | initial_presentation_delay: not present<br>
| | | config OBUs:<br>
| | | <br>
| | | Box: pixi -----<br>
| | | size: 16 (header size: 12)<br>
| | | version: 0<br>
| | | flags: 0<br>
| | | bits_per_channel: 8,8,8<br>
| | | <br>
| | | Box: ispe -----<br>
| | | size: 20 (header size: 12)<br>
| | | version: 0<br>
| | | flags: 0<br>
| | | image width: 512<br>
| | | image height: 512<br>
| | | <br>
| | | Box: colr -----<br>
| | | size: 19 (header size: 8)<br>
| | | colour_type: nclx<br>
| | | colour_primaries: 1<br>
| | | transfer_characteristics: 13<br>
| | | matrix_coefficients: 1<br>
| | | full_range_flag: 1<br>
| | <br>
| | Box: ipma -----<br>
| | size: 49 (header size: 12)<br>
| | version: 0<br>
| | flags: 0<br>
| | associations for item ID: 1<br>
| | | property index: 1 (essential: false)<br>
| | | property index: 2 (essential: false)<br>
| | | property index: 3 (essential: true)<br>
| | | property index: 4 (essential: true)<br>
| | associations for item ID: 2<br>
| | | property index: 1 (essential: false)<br>
| | | property index: 2 (essential: false)<br>
| | | property index: 3 (essential: true)<br>
| | | property index: 4 (essential: true)<br>
| | associations for item ID: 3<br>
| | | property index: 1 (essential: false)<br>
| | | property index: 2 (essential: false)<br>
| | | property index: 3 (essential: true)<br>
| | | property index: 4 (essential: true)<br>
| | associations for item ID: 4<br>
| | | property index: 1 (essential: false)<br>
| | | property index: 2 (essential: false)<br>
| | | property index: 3 (essential: true)<br>
| | | property index: 4 (essential: true)<br>
| | associations for item ID: 5<br>
| | | property index: 5 (essential: false)<br>
| | | property index: 6 (essential: true)<br>
| <br>
| Box: idat -----<br>
| size: 16 (header size: 8)<br>
| number of data bytes: 8<br>
<br>
Box: mdat -----<br>
size: 5388 (header size: 8)<br>
MIME type: image/avif<br>
</p>
</details>

This example illustrates the order that the image grid tiles would be copied to the output image:

<img src="images/Image grid example.png" alt="Image grid example"/>

### Creating image grid tiles

##### Calculating the image grid tile size

While there are multiple methods of picking the optimal image grid tile size, the Paint.NET AvifFileType plugin uses the following algorithm (as of version 1.1.4.0):

1. Pick a maximum tile size based on the compression speed preset.
2. Check if the full image size is greater than the maximum tile size.
3. If the image width or height is greater than the maximum tile size, pick the closest evenly divisible tile size to the maximum tile size.

The following code is a simplified version of the code that is used in the plugin.

```c#
// Although the HEIF specification (ISO/IEC 23008-12:2017) allows an image grid to have up to 256 tiles
// in each direction (65536 total), the ISO base media file format (ISO/IEC 14496-12:2015) limits
// an item reference box to 65535 items.
// Because of this we limit the maximum number of tiles to 250.
//
// While this would result in the image using 62500 tiles in the worst case, it allows
// memory usage to be minimized when encoding extremely wide and/or tall images.
//
// For example, a 65536x65536 pixel image would use a 128x128 grid of 512x512 pixel tiles.
const int MaxTileCount = 250;
// The MIAF specification (ISO/IEC 23000-22:2019) requires that the tile size be at least 64x64 pixels.
const int MinTileSize = 64;

int maxTileSize = 512;

int documentWidth = 4032;
int documentHeight = 3024;

int bestTileColumnCount = 1;
int bestTileWidth = documentWidth;
int bestTileRowCount = 1;
int bestTileHeight = documentHeight;

if (documentWidth > maxTileSize)
{
    for (int tileColumnCount = 2; tileColumnCount <= MaxTileCount; tileColumnCount++)
    {
        int tileWidth = documentWidth / tileColumnCount;

        if (tileWidth < MinTileSize)
        {
            break;
        }

        // The tile must have an even width and be evenly divisible by the document width.
        if ((tileWidth & 1) == 0 && (tileWidth * tileColumnCount) == documentWidth)
        {
            bestTileWidth = tileWidth;
            bestTileColumnCount = tileColumnCount;

            if (tileWidth <= maxTileSize)
            {
                break;
            }
        }
    }
}

if (documentHeight > maxTileSize)
{
    if (documentWidth == documentHeight)
    {
        // Square images use the same number of horizontal and vertical tiles.
        bestTileHeight = bestTileWidth;
        bestTileRowCount = bestTileColumnCount;
    }
    else
    {
        for (int tileRowCount = 2; tileRowCount <= MaxTileCount; tileRowCount++)
        {
            int tileHeight = documentHeight / tileRowCount;

            if (tileHeight < MinTileSize)
            {
                break;
            }

            // The tile must have an even height and be evenly divisible by the document height.
            if ((tileHeight & 1) == 0 && (tileHeight * tileRowCount) == documentHeight)
            {
                bestTileHeight = tileHeight;
                bestTileRowCount = tileRowCount;

                if (tileHeight <= maxTileSize)
                {
                    break;
                }
            }
        }
    }
}
```

When run it calculates that for a maximum tile size of 512x512 pixels a 4032x3024 pixel image would use an 8x6 grid of 504x504 pixel tiles.

##### Encoding the image grid tiles

The following is a pseudo-code example of splitting the main image into tiles, using the output of the previous method.

```c#
for (int row = 0; row < bestTileRowCount; row++)
{
    int y = row * bestTileWidth;
    
    for (int col = 0; col < bestTileColumnCount; col++)
    {
        int x = col * bestTileHeight;

        Rectangle bounds = new Rectangle(x, y, bestTileWidth, bestTileHeight);
        
        EncodeTileImage(image, bounds);
    }
}
WriteImageGridImage();
```

The `EncodeTileImage` method compresses the input image, and writes it to the file with the necessary meta-data for it to be recognized as an image grid tile.
The `WriteImageGridImage` method writes the main image that uses the image grid tiles.

### Encode results

The following table shows my results when encoding a 4032x3024 pixel image on AOM speed 6 with Realtime usage, the image was saved as YUV 4:2:2 using AOM quality 8.
The results will vary based on the image content.

| Image grid setting                            | Encode time | Approximate memory usage (per frame) | Final file size           |
| --------------------------------------------- | ----------- | ------------------------------------ | ------------------------- |
| None, the image was encoded as a single frame | 5 seconds   | 2.5 GB                               | 2.05 MB (2,153,828 bytes) |
| 8x6 grid of 504x504 pixel tiles (48 frames)   | 14 seconds  | 350 MB                               | 2.06 MB (2,170,844 bytes) |

### References


[^1]: [Information technology — High efficiency coding and media delivery in heterogeneous environments — Part 12: Image File Format.](https://www.iso.org/standard/66067.html) International Standard. URL: https://www.iso.org/standard/66067.html    

[^2]: [Information technology -- Multimedia application format (MPEG-A) -- Part 22: Multi-Image Application Format (MiAF).](https://www.iso.org/standard/74417.html)  International Standard. URL: https://www.iso.org/standard/74417.html 

[^3]: [Information technology — Coding of audio-visual objects — Part 12: ISO base media file format.](https://www.iso.org/standard/68960.html) International Standard. URL: https://www.iso.org/standard/68960.html 
