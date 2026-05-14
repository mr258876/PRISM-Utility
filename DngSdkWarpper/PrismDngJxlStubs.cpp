#include "pch.h"

#include "dng_exceptions.h"
#include "dng_host.h"
#include "dng_image.h"
#include "dng_jxl.h"
#include "dng_pixel_buffer.h"
#include "dng_stream.h"

namespace
{
[[noreturn]] void ThrowJxlUnavailable()
{
    ThrowProgramError("JPEG XL support is not compiled into this Project PRISM DNG bridge build.");
}
}

bool ParseJXL(dng_host&, dng_stream&, dng_info&, bool, bool)
{
    return false;
}

dng_jxl_decoder::~dng_jxl_decoder() = default;

void dng_jxl_decoder::Decode(dng_host&, dng_stream&)
{
    ThrowJxlUnavailable();
}

void dng_jxl_decoder::ProcessExifBox(dng_host&, const std::vector<uint8>&)
{
}

void dng_jxl_decoder::ProcessXMPBox(dng_host&, const std::vector<uint8>&)
{
}

void dng_jxl_decoder::ProcessBox(dng_host&, const dng_string&, const std::vector<uint8>&)
{
}

void EncodeJXL_Tile(dng_host&, dng_stream&, const dng_pixel_buffer&, const dng_jxl_color_space_info&, const dng_jxl_encode_settings&)
{
    ThrowJxlUnavailable();
}

void EncodeJXL_Tile(dng_host&, dng_stream&, const dng_image&, const dng_jxl_color_space_info&, const dng_jxl_encode_settings&)
{
    ThrowJxlUnavailable();
}

void EncodeJXL_Container(dng_host&, dng_stream&, const dng_image&, const dng_jxl_encode_settings&, const dng_jxl_color_space_info&, const dng_metadata*, const bool, const bool, const bool, const dng_bmff_box_list*)
{
    ThrowJxlUnavailable();
}

void EncodeJXL_Container(dng_host&, dng_stream&, const dng_pixel_buffer&, const dng_jxl_encode_settings&, const dng_jxl_color_space_info&, const dng_metadata*, const bool, const bool, const bool, const dng_bmff_box_list*)
{
    ThrowJxlUnavailable();
}

real32 JXLQualityToDistance(uint32 quality)
{
    return quality >= 13 ? 0.0f : 1.0f;
}

dng_jxl_encode_settings* JXLQualityToSettings(uint32 quality)
{
    auto* settings = new dng_jxl_encode_settings();
    if (quality >= 13)
    {
        settings->SetDistance(0.0f);
    }
    return settings;
}

void PreviewColorSpaceToJXLEncoding(const PreviewColorSpaceEnum, const uint32, dng_jxl_color_space_info&)
{
    ThrowJxlUnavailable();
}

bool SupportsJXL(const dng_image&)
{
    return false;
}
