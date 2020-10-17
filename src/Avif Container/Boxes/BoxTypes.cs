////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

namespace AvifFileType.AvifContainer
{
    internal static class BoxTypes
    {
        public static readonly FourCC AuxiliaryTypeProperty = new FourCC('a', 'u', 'x', 'C');
        public static readonly FourCC AV1Config = new FourCC('a', 'v', '1', 'C');
        public static readonly FourCC FileType = new FourCC('f', 't', 'y', 'p');
        public static readonly FourCC CleanAperture = new FourCC('c', 'l', 'a', 'p');
        public static readonly FourCC ColorInformation = new FourCC('c', 'o', 'l', 'r');
        public static readonly FourCC Free = new FourCC('f', 'r', 'e', 'e');
        public static readonly FourCC Handler = new FourCC('h', 'd', 'l', 'r');
        public static readonly FourCC ImageMirror = new FourCC('i', 'm', 'i', 'r');
        public static readonly FourCC ImageRotation = new FourCC('i', 'r', 'o', 't');
        public static readonly FourCC ItemProperties = new FourCC('i', 'p', 'r', 'p');
        public static readonly FourCC ItemPropertyAssociations = new FourCC('i', 'p', 'm', 'a');
        public static readonly FourCC ItemPropertyContainer = new FourCC('i', 'p', 'c', 'o');
        public static readonly FourCC ImageSpatialExtents = new FourCC('i', 's', 'p', 'e');
        public static readonly FourCC ItemData = new FourCC('i', 'd', 'a', 't');
        public static readonly FourCC ItemInfo = new FourCC('i', 'i', 'n', 'f');
        public static readonly FourCC ItemInfoEntry = new FourCC('i', 'n', 'f', 'e');
        public static readonly FourCC ItemLocation = new FourCC('i', 'l', 'o', 'c');
        public static readonly FourCC ItemReference = new FourCC('i', 'r', 'e', 'f');
        public static readonly FourCC MediaData = new FourCC('m', 'd', 'a', 't');
        public static readonly FourCC Meta = new FourCC('m', 'e', 't', 'a');
        public static readonly FourCC PrimaryItem = new FourCC('p', 'i', 't', 'm');
        public static readonly FourCC PixelAspectRatio = new FourCC('p', 'a', 's', 'p');
        public static readonly FourCC PixelInformation = new FourCC('p', 'i', 'x', 'i');
        public static readonly FourCC Skip = new FourCC('s', 'k', 'i', 'p');
        public static readonly FourCC Uuid = new FourCC('u', 'u', 'i', 'd');
    }
}
