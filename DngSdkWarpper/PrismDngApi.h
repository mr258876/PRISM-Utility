#pragma once

#include <stdint.h>

#ifdef DNGSDKWARPPER_EXPORTS
#define PRISM_DNG_API __declspec(dllexport)
#else
#define PRISM_DNG_API __declspec(dllimport)
#endif

extern "C" {

enum PrismDngStatus
{
    PRISM_DNG_STATUS_OK = 0,
    PRISM_DNG_STATUS_INVALID_ARGUMENT = 1,
    PRISM_DNG_STATUS_NOT_IMPLEMENTED = 2,
    PRISM_DNG_STATUS_UNSUPPORTED_FORMAT = 3,
    PRISM_DNG_STATUS_IO_ERROR = 4,
    PRISM_DNG_STATUS_INTERNAL_ERROR = 5
};

enum PrismDngAbiVersion
{
    PRISM_DNG_ABI_VERSION_1 = 1,
    PRISM_DNG_ABI_VERSION_2 = 2
};

static const uint32_t PRISM_DNG_MAX_MASKED_AREAS = 4;
static const uint32_t PRISM_DNG_MAX_BLACK_PLANES = 4;

enum PrismDngPixelLayout
{
    PRISM_DNG_PIXEL_LAYOUT_UNKNOWN = 0,
    PRISM_DNG_PIXEL_LAYOUT_RAW_MOSAIC = 1,
    PRISM_DNG_PIXEL_LAYOUT_LINEAR_RGB = 2,
    PRISM_DNG_PIXEL_LAYOUT_MONOCHROME_RAW = 3,
    PRISM_DNG_PIXEL_LAYOUT_LINEAR_RAW_MULTI_CHANNEL = 4
};

enum PrismDngCfaPattern
{
    PRISM_DNG_CFA_UNKNOWN = 0,
    PRISM_DNG_CFA_RGGB = 1,
    PRISM_DNG_CFA_BGGR = 2,
    PRISM_DNG_CFA_GRBG = 3,
    PRISM_DNG_CFA_GBRG = 4
};

struct PrismDngRectangle
{
    uint32_t top;
    uint32_t left;
    uint32_t bottom;
    uint32_t right;
};

struct PrismDngRational64
{
    uint32_t numerator;
    uint32_t denominator;
};

struct PrismDngImageBuffer
{
    const void* data;
    uint64_t dataBytes;
    uint32_t width;
    uint32_t height;
    uint32_t rowStrideBytes;
    uint16_t bitsPerSample;
    uint16_t samplesPerPixel;
    uint32_t pixelLayout;
    uint32_t cfaPattern;
};

struct PrismDngColorMetadata
{
    double analogBalance[3];
    double cameraNeutral[3];
    double colorMatrix1[9];
    double colorMatrix2[9];
    uint8_t hasAnalogBalance;
    uint8_t hasCameraNeutral;
    uint8_t hasColorMatrix1;
    uint8_t hasColorMatrix2;
};

struct PrismDngBlackLevelPlane
{
    double topLeft;
    double topRight;
    double bottomLeft;
    double bottomRight;
};

struct PrismDngDateTime
{
    uint32_t year;
    uint32_t month;
    uint32_t day;
    uint32_t hour;
    uint32_t minute;
    uint32_t second;
    int32_t offsetMinutes;
    uint32_t hasDateTime;
};

struct PrismDngMetadata
{
    const wchar_t* make;
    const wchar_t* model;
    const wchar_t* software;
    const wchar_t* uniqueCameraModel;
    uint32_t isoSpeed;
    PrismDngRational64 exposureTime;
    PrismDngRational64 frameRate;
    uint32_t blackLevel;
    uint32_t whiteLevel;
    PrismDngDateTime captureTime;
    PrismDngRectangle activeArea;
    PrismDngRectangle defaultCrop;
    PrismDngRectangle maskedAreas[PRISM_DNG_MAX_MASKED_AREAS];
    uint32_t maskedAreaCount;
    PrismDngBlackLevelPlane blackLevelPlanes[PRISM_DNG_MAX_BLACK_PLANES];
    uint32_t blackLevelPlaneCount;
    PrismDngColorMetadata color;
    uint32_t channelColors[4];
    uint8_t hasChannelColors;
};

struct PrismDngWriteRequestV1
{
    uint32_t structSize;
    uint32_t abiVersion;
    uint32_t flags;
    uint32_t reserved0;
    const wchar_t* outputPath;
    PrismDngImageBuffer image;
    PrismDngMetadata metadata;
    uint64_t reserved[6];
};

struct PrismDngWriteRequestV2
{
    uint32_t structSize;
    uint32_t abiVersion;
    uint32_t flags;
    uint32_t reserved0;
    const wchar_t* outputPath;
    PrismDngImageBuffer image;
    PrismDngMetadata metadata;
    uint64_t reserved[6];
};

PRISM_DNG_API int PrismDngWriteFromBuffer(const PrismDngWriteRequestV2* request);

PRISM_DNG_API int PrismDngGetLastErrorMessage(
    wchar_t* buffer,
    uint32_t bufferChars);

}
