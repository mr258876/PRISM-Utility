#pragma once

#include <string>

#include "PrismDngApi.h"

class PrismDngWriter
{
public:
    int WriteFromBuffer(const PrismDngWriteRequestV2& request) const;

private:
    static bool IsRectangleEmpty(const PrismDngRectangle& rectangle);
    static bool IsRectangleWithinImage(const PrismDngRectangle& rectangle, uint32_t width, uint32_t height);
};
