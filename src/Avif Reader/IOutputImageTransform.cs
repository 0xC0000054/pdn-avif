////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020-2025 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

namespace AvifFileType
{
    interface IOutputImageTransform
    {
        void Crop(AvifContainer.CleanApertureBox cleanApertureBox);

        void Rotate90CCW();

        void Rotate180();

        void Rotate270CCW();

        void FlipHorizontal();

        void FlipVertical();
    }
}
