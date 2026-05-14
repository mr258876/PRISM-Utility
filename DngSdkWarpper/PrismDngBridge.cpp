#include "pch.h"

#include "PrismDngApi.h"

#include <algorithm>
#include <cstring>
#include <cwchar>
#include <exception>
#include <string>

#include "PrismDngError.h"
#include "PrismDngWriter.h"

namespace
{
std::wstring FromUtf8ExceptionMessage(const std::exception& exception)
{
    const auto* narrow = exception.what();
    if (narrow == nullptr)
    {
        return L"Unknown exception.";
    }

    std::wstring widened;
    widened.reserve(std::strlen(narrow));
    for (const auto* cursor = narrow; *cursor != '\0'; ++cursor)
    {
        widened.push_back(static_cast<unsigned char>(*cursor));
    }

    return widened;
}
}

int PrismDngWriteFromBuffer(const PrismDngWriteRequestV2* request)
{
    if (request == nullptr)
    {
        PrismDngSetLastErrorMessage(L"Request pointer must not be null.");
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    try
    {
        PrismDngWriter writer;
        return writer.WriteFromBuffer(*request);
    }
    catch (const std::exception& exception)
    {
        PrismDngSetLastErrorMessage(FromUtf8ExceptionMessage(exception));
        return PRISM_DNG_STATUS_INTERNAL_ERROR;
    }
    catch (...)
    {
        PrismDngSetLastErrorMessage(L"Unknown internal error while preparing DNG output.");
        return PRISM_DNG_STATUS_INTERNAL_ERROR;
    }
}

int PrismDngGetLastErrorMessage(wchar_t* buffer, uint32_t bufferChars)
{
    if (buffer == nullptr || bufferChars == 0)
    {
        return PRISM_DNG_STATUS_INVALID_ARGUMENT;
    }

    const auto& message = PrismDngGetLastErrorStorage();
    const auto maxWritable = static_cast<size_t>(bufferChars - 1);
    const auto copyLength = (std::min)(message.size(), maxWritable);

    if (copyLength > 0)
    {
        std::wmemcpy(buffer, message.c_str(), copyLength);
    }

    buffer[copyLength] = L'\0';
    return PRISM_DNG_STATUS_OK;
}
