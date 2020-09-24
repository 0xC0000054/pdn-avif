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

#pragma once

#include "aom/aom_codec.h"
#include <stdexcept>

class codec_error : public std::runtime_error
{
public:
    codec_error(const char* message) : std::runtime_error(message)
    {
        error = AOM_CODEC_ERROR;
    }

    codec_error(aom_codec_err_t err) : std::runtime_error(aom_codec_err_to_string(err))
    {
        error = err;
    }

    aom_codec_err_t get_error_code() const noexcept
    {
        return error;
    }

private:
    aom_codec_err_t error;
};

class ScopedAOMCodec
{
public:
    aom_codec_ctx_t* get() noexcept
    {
        if (initialized)
        {
            return &codec;
        }
        else
        {
            return nullptr;
        }
    }

protected:
    ScopedAOMCodec() : codec(), initialized(false)
    {
    }

    ~ScopedAOMCodec() noexcept
    {
        if (initialized)
        {
            initialized = false;
            aom_codec_destroy(&codec);
        }
    }

    static void throw_on_error(aom_codec_err_t err)
    {
        if (err != AOM_CODEC_OK)
        {
            if (err == AOM_CODEC_MEM_ERROR)
            {
                throw new std::bad_alloc();
            }
            else
            {
                throw new codec_error(err);
            }
        }
    }

    aom_codec_ctx_t codec;
    bool initialized;
};
