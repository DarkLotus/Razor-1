#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <assert.h>
#include <winsock2.h>
#include <time.h>
#include <zmouse.h>
#include <uxtheme.h>
#include <vssym32.h>
#include <dwmapi.h>

#define DLLFUNCTION __declspec(dllexport)

DLLFUNCTION HBITMAP CaptureScreen(HWND hUOWindow, BOOL full, const char *msg);
DLLFUNCTION void BringToFront(HWND hWnd);
DLLFUNCTION bool AllowBit(uint64_t bit);
DLLFUNCTION void HandleNegotiate(uint64_t features);
DLLFUNCTION void InitTitleBar(const char *datapath);
DLLFUNCTION void DrawTitleBar(HWND hUOWindow, const char *str);
DLLFUNCTION void FreeTitleBar();
DLLFUNCTION void CreateUOAWindow(HWND razorWindow);
DLLFUNCTION void DestroyUOAWindow();
