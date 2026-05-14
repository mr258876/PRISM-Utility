#include "pch.h"

#include "PrismDngError.h"

namespace
{
thread_local std::wstring g_lastErrorMessage;
}

void PrismDngSetLastErrorMessage(const std::wstring& message)
{
    g_lastErrorMessage = message;
}

const std::wstring& PrismDngGetLastErrorStorage()
{
    return g_lastErrorMessage;
}
