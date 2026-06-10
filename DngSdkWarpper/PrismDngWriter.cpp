#include "pch.h"

#include "PrismDngWriter.h"

#include <cstring>
#include <stdexcept>
#include <string>

#include "dng_camera_profile.h"
#include "dng_date_time.h"
#include "dng_exif.h"
#include "dng_file_stream.h"
#include "dng_host.h"
#include "dng_image_writer.h"
#include "dng_matrix.h"
#include "dng_sdk_limits.h"
#include "dng_negative.h"
#include "dng_pixel_buffer.h"
#include "dng_rect.h"
#include "dng_simple_image.h"
#include "dng_stream.h"
#include "dng_string.h"
#include "dng_tag_types.h"
#include "dng_tag_values.h"

#include "PrismDngError.h"

namespace
{
bool IsSupportedBitsPerSample(uint16_t bitsPerSample)
{
    return bitsPerSample == 8 || bitsPerSample == 16;
}

bool HasEnoughBytes(const PrismDngImageBuffer& image)
{
    if (image.height == 0)
    {
        return false;
    }

    const auto requiredBytes = static_cast<uint64_t>(image.rowStrideBytes) * static_cast<uint64_t>(image.height);
    return requiredBytes <= image.dataBytes;
}

uint32 ResolvePixelType(uint16 bitsPerSample)
{
    switch (bitsPerSample)
    {
    case 8:
        return ttByte;
    case 16:
        return ttShort;
    default:
        throw std::invalid_argument("Unsupported bitsPerSample.");
    }
}

uint32 ResolveBayerPhase(uint32 cfaPattern)
{
    switch (cfaPattern)
    {
    case PRISM_DNG_CFA_GRBG:
        return 0;
    case PRISM_DNG_CFA_RGGB:
        return 1;
    case PRISM_DNG_CFA_BGGR:
        return 2;
    case PRISM_DNG_CFA_GBRG:
        return 3;
    default:
        throw std::invalid_argument("Unsupported CFA pattern.");
    }
}

bool IsMonochromeRaw(const PrismDngImageBuffer& image)
{
    return image.pixelLayout == PRISM_DNG_PIXEL_LAYOUT_MONOCHROME_RAW;
}

bool IsRawMosaic(const PrismDngImageBuffer& image)
{
    return image.pixelLayout == PRISM_DNG_PIXEL_LAYOUT_RAW_MOSAIC;
}

bool IsLinearRgb(const PrismDngImageBuffer& image)
{
    return image.pixelLayout == PRISM_DNG_PIXEL_LAYOUT_LINEAR_RGB;
}

bool IsLinearRawMultiChannel(const PrismDngImageBuffer& image)
{
    return image.pixelLayout == PRISM_DNG_PIXEL_LAYOUT_LINEAR_RAW_MULTI_CHANNEL;
}

uint32 ResolveWhiteLevel(uint16 bitsPerSample, uint32 configuredWhiteLevel)
{
    if (configuredWhiteLevel != 0)
    {
        return configuredWhiteLevel;
    }

    return bitsPerSample >= 16
        ? 65535u
        : ((1u << bitsPerSample) - 1u);
}

dng_rect ToDngRect(const PrismDngRectangle& rectangle)
{
    return dng_rect(
        static_cast<int32>(rectangle.top),
        static_cast<int32>(rectangle.left),
        static_cast<int32>(rectangle.bottom),
        static_cast<int32>(rectangle.right));
}

void SetDngString(dng_string& destination, const wchar_t* source)
{
    if (source == nullptr || source[0] == L'\0')
    {
        destination.Clear();
        return;
    }

    static_assert(sizeof(wchar_t) == sizeof(uint16), "Windows wide strings must be UTF-16.");
    destination.Set_UTF16(reinterpret_cast<const uint16*>(source));
}

void SetDngModelName(dng_negative& negative, const wchar_t* source)
{
    if (source == nullptr || source[0] == L'\0')
    {
        return;
    }

    std::string modelName;
    for (const auto* cursor = source; *cursor != L'\0'; ++cursor)
    {
        modelName.push_back(static_cast<char>(*cursor));
    }

    negative.SetModelName(modelName.c_str());
}

dng_urational ToDngUrational(const PrismDngRational64& value)
{
    if (value.denominator == 0)
    {
        throw std::invalid_argument("Rational denominator must not be zero.");
    }

    return dng_urational(value.numerator, value.denominator);
}

void CopyIntoImage(dng_simple_image& image, const PrismDngImageBuffer& source)
{
    dng_pixel_buffer destination;
    image.GetPixelBuffer(destination);

    const auto bytesPerSample = static_cast<size_t>(source.bitsPerSample / 8u);
    const auto targetRowBytes = static_cast<size_t>(source.width) * static_cast<size_t>(source.samplesPerPixel) * bytesPerSample;

    for (uint32 row = 0; row < source.height; ++row)
    {
        auto* targetRow = static_cast<uint8*>(destination.DirtyPixel(static_cast<int32>(row), 0));
        const auto* sourceRow = static_cast<const uint8*>(source.data) + (static_cast<size_t>(row) * source.rowStrideBytes);
        std::memcpy(targetRow, sourceRow, targetRowBytes);
    }
}

void ApplyColorMetadata(dng_negative& negative, const PrismDngColorMetadata& color)
{
    if (color.hasAnalogBalance != 0)
    {
        dng_vector analogBalance(3);
        for (uint32 index = 0; index < 3; ++index)
        {
            analogBalance[index] = color.analogBalance[index];
        }
        negative.SetAnalogBalance(analogBalance);
    }

    if (color.hasCameraNeutral != 0)
    {
        dng_vector cameraNeutral(3);
        for (uint32 index = 0; index < 3; ++index)
        {
            cameraNeutral[index] = color.cameraNeutral[index];
        }
        negative.SetCameraNeutral(cameraNeutral);
    }

    if (color.hasColorMatrix1 != 0 || color.hasColorMatrix2 != 0)
    {
        AutoPtr<dng_camera_profile> profile(new dng_camera_profile());
        profile->SetName("Project PRISM");
        profile->SetCalibrationIlluminant1(lsD65);

        if (color.hasColorMatrix1 != 0)
        {
            dng_matrix colorMatrix1(3, 3);
            for (uint32 row = 0; row < 3; ++row)
            {
                for (uint32 column = 0; column < 3; ++column)
                {
                    colorMatrix1[row][column] = color.colorMatrix1[row * 3 + column];
                }
            }
            profile->SetColorMatrix1(colorMatrix1);
        }

        if (color.hasColorMatrix2 != 0)
        {
            dng_matrix colorMatrix2(3, 3);
            for (uint32 row = 0; row < 3; ++row)
            {
                for (uint32 column = 0; column < 3; ++column)
                {
                    colorMatrix2[row][column] = color.colorMatrix2[row * 3 + column];
                }
            }
            profile->SetColorMatrix2(colorMatrix2);
        }

        negative.AddProfile(profile);
    }
}

dng_matrix BuildFourChannelColorMatrix(const PrismDngMetadata& metadata)
{
    dng_matrix matrix(4, 3);
    for (uint32 row = 0; row < 4; ++row)
    {
        const uint32 key = metadata.channelColors[row];
        switch (key)
        {
        case colorKeyRed:
            matrix[row][0] = 1.0; matrix[row][1] = 0.0; matrix[row][2] = 0.0;
            break;
        case colorKeyGreen:
            matrix[row][0] = 0.0; matrix[row][1] = 1.0; matrix[row][2] = 0.0;
            break;
        case colorKeyBlue:
            matrix[row][0] = 0.0; matrix[row][1] = 0.0; matrix[row][2] = 1.0;
            break;
        case colorKeyCyan:
            matrix[row][0] = 0.0; matrix[row][1] = 1.0; matrix[row][2] = 1.0;
            break;
        case colorKeyMagenta:
            matrix[row][0] = 1.0; matrix[row][1] = 0.0; matrix[row][2] = 1.0;
            break;
        case colorKeyYellow:
            matrix[row][0] = 1.0; matrix[row][1] = 1.0; matrix[row][2] = 0.0;
            break;
        case colorKeyWhite:
        default:
            matrix[row][0] = 1.0; matrix[row][1] = 1.0; matrix[row][2] = 1.0;
            break;
        }
    }
    return matrix;
}

void ApplyLinearRawMultiChannelMetadata(dng_negative& negative, const PrismDngMetadata& metadata)
{
    negative.SetColorChannels(4);
    if (metadata.hasChannelColors != 0)
    {
        negative.SetColorKeys(
            static_cast<ColorKeyCode>(metadata.channelColors[0]),
            static_cast<ColorKeyCode>(metadata.channelColors[1]),
            static_cast<ColorKeyCode>(metadata.channelColors[2]),
            static_cast<ColorKeyCode>(metadata.channelColors[3]));
    }
    else
    {
        negative.SetGMCY();
    }

    AutoPtr<dng_camera_profile> profile(new dng_camera_profile());
    profile->SetName("Project PRISM LinearRaw4");
    profile->SetCalibrationIlluminant1(lsD65);
    profile->SetColorMatrix1(BuildFourChannelColorMatrix(metadata));
    negative.AddProfile(profile);
}

void ApplyMaskedAreas(dng_negative& negative, const PrismDngMetadata& metadata)
{
    if (metadata.maskedAreaCount == 0)
    {
        return;
    }

    dng_rect maskedAreas[kMaxMaskedAreas];
    for (uint32 index = 0; index < metadata.maskedAreaCount; ++index)
    {
        maskedAreas[index] = ToDngRect(metadata.maskedAreas[index]);
    }

    negative.SetMaskedAreas(metadata.maskedAreaCount, maskedAreas);
}

void ApplyBlackLevels(dng_negative& negative, const PrismDngMetadata& metadata)
{
    if (metadata.blackLevelPlaneCount > 0)
    {
        for (uint32 plane = 0; plane < metadata.blackLevelPlaneCount; ++plane)
        {
            const auto& black = metadata.blackLevelPlanes[plane];
            negative.SetQuadBlacks(
                black.topLeft,
                black.topRight,
                black.bottomLeft,
                black.bottomRight,
                static_cast<int32>(plane));
        }

        return;
    }

    negative.SetBlackLevel(metadata.blackLevel);
}

bool IsValidCaptureTime(const PrismDngDateTime& captureTime)
{
    if (captureTime.hasDateTime == 0)
    {
        return false;
    }

    const dng_date_time dateTime(
        captureTime.year,
        captureTime.month,
        captureTime.day,
        captureTime.hour,
        captureTime.minute,
        captureTime.second);
    return dateTime.IsValid();
}

void ApplyCaptureTime(dng_exif& exif, const PrismDngDateTime& captureTime)
{
    if (!IsValidCaptureTime(captureTime))
    {
        return;
    }

    dng_date_time_info dateTimeInfo;
    dateTimeInfo.SetDateTime(dng_date_time(
        captureTime.year,
        captureTime.month,
        captureTime.day,
        captureTime.hour,
        captureTime.minute,
        captureTime.second));

    dng_time_zone timeZone;
    timeZone.SetOffsetMinutes(captureTime.offsetMinutes);
    if (timeZone.IsValid())
    {
        dateTimeInfo.SetZone(timeZone);
        exif.SetVersion0231();
    }

    exif.fDateTime = dateTimeInfo;
    exif.fDateTimeOriginal = dateTimeInfo;
    exif.fDateTimeDigitized = dateTimeInfo;
}

void ApplyMetadata(dng_negative& negative, const PrismDngImageBuffer& image, const PrismDngMetadata& metadata)
{
    const auto hasActiveArea = !(metadata.activeArea.top == 0 && metadata.activeArea.left == 0
        && metadata.activeArea.bottom == 0 && metadata.activeArea.right == 0);
    const auto hasDefaultCrop = !(metadata.defaultCrop.top == 0 && metadata.defaultCrop.left == 0
        && metadata.defaultCrop.bottom == 0 && metadata.defaultCrop.right == 0);
    const PrismDngRectangle activeArea = hasActiveArea
        ? metadata.activeArea
        : PrismDngRectangle{ 0, 0, image.height, image.width };

    negative.SetActiveArea(ToDngRect(activeArea));

    if (hasDefaultCrop)
    {
        negative.SetDefaultCropOrigin(
            metadata.defaultCrop.left - activeArea.left,
            metadata.defaultCrop.top - activeArea.top);
        negative.SetDefaultCropSize(
            metadata.defaultCrop.right - metadata.defaultCrop.left,
            metadata.defaultCrop.bottom - metadata.defaultCrop.top);
    }
    else
    {
        negative.SetDefaultCropOrigin(0, 0);
        negative.SetDefaultCropSize(
            activeArea.right - activeArea.left,
            activeArea.bottom - activeArea.top);
    }

    negative.SetRawDefaultCrop();
    ApplyMaskedAreas(negative, metadata);
    ApplyBlackLevels(negative, metadata);
    negative.SetWhiteLevel(ResolveWhiteLevel(image.bitsPerSample, metadata.whiteLevel));

    if (IsMonochromeRaw(image))
    {
        negative.SetMonochrome();
    }
    else if (IsLinearRawMultiChannel(image))
    {
        ApplyLinearRawMultiChannelMetadata(negative, metadata);
    }
    else if (IsLinearRgb(image))
    {
        negative.SetColorChannels(3);
        negative.SetColorKeys(colorKeyRed, colorKeyGreen, colorKeyBlue);
    }
    else
    {
        negative.SetColorChannels(3);
        negative.SetColorKeys(colorKeyRed, colorKeyGreen, colorKeyBlue);
        negative.SetBayerMosaic(ResolveBayerPhase(image.cfaPattern));
    }

    dng_exif& exif = negative.Metadata().Exif<dng_exif>();
    SetDngString(exif.fMake, metadata.make);
    SetDngString(exif.fModel, metadata.model);
    SetDngString(exif.fSoftware, metadata.software);
    ApplyCaptureTime(exif, metadata.captureTime);

    if (metadata.uniqueCameraModel != nullptr && metadata.uniqueCameraModel[0] != L'\0')
    {
        SetDngModelName(negative, metadata.uniqueCameraModel);
    }
    else if (metadata.model != nullptr && metadata.model[0] != L'\0')
    {
        SetDngModelName(negative, metadata.model);
    }

    exif.fISOSpeedRatings[0] = metadata.isoSpeed;
    exif.fISOSpeed = metadata.isoSpeed;

    if (metadata.exposureTime.denominator != 0)
    {
        exif.fExposureTime = ToDngUrational(metadata.exposureTime);
    }

    if (!IsMonochromeRaw(image) && !IsLinearRawMultiChannel(image))
    {
        ApplyColorMetadata(negative, metadata.color);
    }
    negative.SynchronizeMetadata();
}

std::wstring NarrowExceptionToWide(const std::exception& exception)
{
    std::wstring message;
    const auto* narrow = exception.what();
    if (narrow == nullptr)
    {
        return L"Unknown native exception.";
    }

    message.reserve(std::strlen(narrow));
    for (const auto* cursor = narrow; *cursor != '\0'; ++cursor)
    {
        message.push_back(static_cast<unsigned char>(*cursor));
    }

    return message;
}
}

int PrismDngWriter::WriteFromBuffer(const PrismDngWriteRequestV2& request) const
{
    if (request.structSize < sizeof(PrismDngWriteRequestV2))
    {
        PrismDngSetLastErrorMessage(L"Request structSize is smaller than PrismDngWriteRequestV2.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (request.abiVersion != PRISM_DNG_ABI_VERSION_2)
    {
        PrismDngSetLastErrorMessage(L"Unsupported DNG bridge ABI version.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    const auto outputPath = std::wstring(request.outputPath == nullptr ? L"" : request.outputPath);
    const auto& image = request.image;
    const auto& metadata = request.metadata;

    if (outputPath.empty())
    {
        PrismDngSetLastErrorMessage(L"Output path must not be empty.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (image.data == nullptr)
    {
        PrismDngSetLastErrorMessage(L"Image buffer pointer must not be null.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (image.width == 0 || image.height == 0 || image.rowStrideBytes == 0 || image.dataBytes == 0)
    {
        PrismDngSetLastErrorMessage(L"Image dimensions and buffer sizes must be non-zero.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (!IsSupportedBitsPerSample(image.bitsPerSample))
    {
        PrismDngSetLastErrorMessage(L"Only 8-bit and 16-bit source buffers are currently supported.");
        return PRISM_DNG_STATUS_UNSUPPORTED_FORMAT;
    }

    if (!HasEnoughBytes(image))
    {
        PrismDngSetLastErrorMessage(L"Image buffer is smaller than width, height, and stride require.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (!IsRawMosaic(image) && !IsLinearRgb(image) && !IsMonochromeRaw(image) && !IsLinearRawMultiChannel(image))
    {
        PrismDngSetLastErrorMessage(L"The current writer only supports raw mosaic, linear RGB, monochrome raw, or multi-channel linear raw input for DNG writing.");
        return PRISM_DNG_STATUS_UNSUPPORTED_FORMAT;
    }

    if ((IsRawMosaic(image) || IsMonochromeRaw(image)) && image.samplesPerPixel != 1)
    {
        PrismDngSetLastErrorMessage(L"Raw mosaic and monochrome raw input must have exactly one sample per pixel.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (IsLinearRgb(image) && image.samplesPerPixel != 3)
    {
        PrismDngSetLastErrorMessage(L"Linear RGB export requires exactly three samples per pixel.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (IsLinearRawMultiChannel(image) && image.samplesPerPixel != 4)
    {
        PrismDngSetLastErrorMessage(L"Multi-channel linear raw export currently requires exactly four samples per pixel.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (IsRawMosaic(image) && image.cfaPattern == PRISM_DNG_CFA_UNKNOWN)
    {
        PrismDngSetLastErrorMessage(L"A concrete CFA pattern is required for raw mosaic DNG output.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (!IsRectangleEmpty(metadata.activeArea)
        && !IsRectangleWithinImage(metadata.activeArea, image.width, image.height))
    {
        PrismDngSetLastErrorMessage(L"Active area must lie within the source image bounds.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (!IsRectangleEmpty(metadata.defaultCrop)
        && !IsRectangleWithinImage(metadata.defaultCrop, image.width, image.height))
    {
        PrismDngSetLastErrorMessage(L"Default crop must lie within the source image bounds.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    if (metadata.maskedAreaCount > PRISM_DNG_MAX_MASKED_AREAS)
    {
        PrismDngSetLastErrorMessage(L"Masked area count exceeds current SDK bridge limit.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    for (uint32 index = 0; index < metadata.maskedAreaCount; ++index)
    {
        if (!IsRectangleWithinImage(metadata.maskedAreas[index], image.width, image.height))
        {
            PrismDngSetLastErrorMessage(L"Masked areas must lie within the source image bounds.");
            return PRISM_DNG_STATUS_INVALID_ARGUMENT;
        }
    }

    if (metadata.blackLevelPlaneCount > PRISM_DNG_MAX_BLACK_PLANES || metadata.blackLevelPlaneCount > image.samplesPerPixel)
    {
        PrismDngSetLastErrorMessage(L"Black-level plane count exceeds the supported range.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    try
    {
        dng_host host;
        host.SetSaveDNGVersion(dngVersion_SaveDefault);
        host.SetSaveLinearDNG(IsLinearRgb(image));
        host.SetKeepOriginalFile(false);
        host.SetLossyMosaicJXL(false);
        host.SetLosslessJXL(false);

        AutoPtr<dng_negative> negative(host.Make_dng_negative());
        AutoPtr<dng_image> stage1Image(host.Make_dng_image(dng_rect(image.height, image.width), image.samplesPerPixel, ResolvePixelType(image.bitsPerSample)));

        auto* simpleImage = dynamic_cast<dng_simple_image*>(stage1Image.Get());
        if (simpleImage == nullptr)
        {
            PrismDngSetLastErrorMessage(L"Host image factory did not return dng_simple_image.");
            return PRISM_DNG_STATUS_INTERNAL_ERROR;
        }

        CopyIntoImage(*simpleImage, image);
        negative->SetStage1Image(stage1Image);
        ApplyMetadata(*negative.Get(), image, metadata);

        dng_file_stream stream(outputPath.c_str(), true);
        dng_image_writer writer;
        writer.WriteDNG(host, stream, *negative.Get(), nullptr, dngVersion_SaveDefault, true);
        stream.Flush();

        PrismDngSetLastErrorMessage(L"");
        return PRISM_DNG_STATUS_OK;
    }
    catch (const std::exception& exception)
    {
        PrismDngSetLastErrorMessage(std::wstring(L"DNG write failed: ") + NarrowExceptionToWide(exception));
        return PRISM_DNG_STATUS_INTERNAL_ERROR;
    }
    catch (...)
    {
        PrismDngSetLastErrorMessage(L"DNG write failed with an unknown native exception.");
        return PRISM_DNG_STATUS_INTERNAL_ERROR;
    }
}

bool PrismDngWriter::IsRectangleEmpty(const PrismDngRectangle& rectangle)
{
    return rectangle.bottom == 0 && rectangle.right == 0 && rectangle.top == 0 && rectangle.left == 0;
}

bool PrismDngWriter::IsRectangleWithinImage(const PrismDngRectangle& rectangle, uint32_t width, uint32_t height)
{
    if (rectangle.bottom <= rectangle.top || rectangle.right <= rectangle.left)
    {
        return false;
    }

    return rectangle.top < height
        && rectangle.left < width
        && rectangle.bottom <= height
        && rectangle.right <= width;
}
