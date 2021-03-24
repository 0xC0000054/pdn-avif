////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

#pragma once

#include <WinSDKVer.h>

// Set the minimum OS version to Windows 7

#define _WIN32_WINNT 0x0601
#define WINVER 0x0601
#define NTDDI_VERSION 0x06010000

#include <SDKDDKVer.h>
