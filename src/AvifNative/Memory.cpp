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

#include "Memory.h"
#include <cstdlib>

namespace AvifMemory
{
    void* Allocate(size_t sizeInBytes)
    {
        return std::malloc(sizeInBytes);
    }

    void Free(void* ptr)
    {
        std::free(ptr);
    }
}
