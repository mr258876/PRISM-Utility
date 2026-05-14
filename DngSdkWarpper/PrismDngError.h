#pragma once

#include <string>

void PrismDngSetLastErrorMessage(const std::wstring& message);
const std::wstring& PrismDngGetLastErrorStorage();
