#define WIN32_LEAN_AND_MEAN 1

#include <iostream>
#include <windows.h>
#include <AccCtrl.h>
#include <AclAPI.h>
#include <iomanip>
#include <sstream>

using namespace std;

struct HEX
{
   HEX(unsigned long num, unsigned long fieldwidth = 8, bool bUpcase = false)
      : m_num(num), m_width(fieldwidth), m_upcase(bUpcase)
   {}

   unsigned long m_num;
   unsigned long m_width;
   bool m_upcase;
};

inline ostream& operator << (ostream& os, const HEX& h)
{
   int fmt = os.flags();
   char fillchar = os.fill('0');
   os << "0x" << hex << (h.m_upcase ? uppercase : nouppercase) << setw(h.m_width) << h.m_num;
   os.fill(fillchar);
   os.flags(fmt);
   return os;
}

inline wostream& operator << (wostream& os, const HEX& h)
{
   int fmt = os.flags();
   wchar_t fillchar = os.fill(L'0');
   os << L"0x" << hex << (h.m_upcase ? uppercase : nouppercase) << setw(h.m_width) << h.m_num;
   os.fill(fillchar);
   os.flags(fmt);
   return os;
}

inline std::string SysErrorMessageWithCode(DWORD dwErrCode /*= GetLastError()*/)
{
   LPWSTR pszErrMsg = NULL;
   std::stringstream sRetval;
   DWORD flags =
      FORMAT_MESSAGE_ALLOCATE_BUFFER |
      FORMAT_MESSAGE_IGNORE_INSERTS |
      FORMAT_MESSAGE_FROM_SYSTEM;

   if (FormatMessageW(
      flags,
      NULL,
      dwErrCode,
      MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), // Default language
      (LPWSTR)&pszErrMsg,
      0,
      NULL))
   {
      sRetval << pszErrMsg << L" (Error # " << dwErrCode << L" = " << HEX(dwErrCode) << L")";
      LocalFree(pszErrMsg);
   }
   else
   {
      sRetval << L"Error # " << dwErrCode << L" (" << HEX(dwErrCode) << L")";
   }
   return sRetval.str();
}

bool RunAsDesktopUser(
   __in    const wchar_t* szApp,
   __in    wchar_t* szCmdLine,
   __in    const wchar_t* szCurrDir,
   __in    LPSECURITY_ATTRIBUTES pSec,
   __in    LPSTARTUPINFOW si,
   __inout LPPROCESS_INFORMATION pi)
{
   HANDLE hShellProcess = NULL, hShellProcessToken = NULL, hPrimaryToken = NULL;
   HWND hwnd = NULL;
   DWORD dwPID = 0;
   BOOL ret;
   DWORD dwLastErr;

   // Enable SeIncreaseQuotaPrivilege in this process.  (This won't work if current process is not elevated.)
   HANDLE hProcessToken = NULL;
   if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES, &hProcessToken))
   {
      dwLastErr = GetLastError();
      cout << L"OpenProcessToken failed:  " << SysErrorMessageWithCode(dwLastErr);
      return false;
   }
   else
   {
      TOKEN_PRIVILEGES tkp;
      tkp.PrivilegeCount = 1;
      LookupPrivilegeValueW(NULL, SE_INCREASE_QUOTA_NAME, &tkp.Privileges[0].Luid);
      tkp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
      AdjustTokenPrivileges(hProcessToken, FALSE, &tkp, 0, NULL, NULL);
      dwLastErr = GetLastError();
      CloseHandle(hProcessToken);
      if (ERROR_SUCCESS != dwLastErr)
      {
         cout << L"AdjustTokenPrivileges failed:  " << SysErrorMessageWithCode(dwLastErr);
         return false;
      }
   }

   // Get an HWND representing the desktop shell.
   // CAVEATS:  This will fail if the shell is not running (crashed or terminated), or the default shell has been
   // replaced with a custom shell.  This also won't return what you probably want if Explorer has been terminated and
   // restarted elevated.
   hwnd = GetShellWindow();
   if (NULL == hwnd)
   {
      cout << L"No desktop shell is present";
      return false;
   }

   // Get the PID of the desktop shell process.
   GetWindowThreadProcessId(hwnd, &dwPID);
   if (0 == dwPID)
   {
      cout << L"Unable to get PID of desktop shell.";
      return false;
   }

   // Open the desktop shell process in order to query it (get the token)
   hShellProcess = OpenProcess(PROCESS_QUERY_INFORMATION, FALSE, dwPID);
   if (!hShellProcess)
   {
      dwLastErr = GetLastError();
      cout << L"Can't open desktop shell process:  " << SysErrorMessageWithCode(dwLastErr);
      return false;
   }

   // Get the process token of the desktop shell.
   ret = OpenProcessToken(hShellProcess, TOKEN_DUPLICATE, &hShellProcessToken);
   if (!ret)
   {
      dwLastErr = GetLastError();
      cout << L"Can't get process token of desktop shell:  " << SysErrorMessageWithCode(dwLastErr);
      return false;
   }

   // Duplicate the shell's process token to get a primary token.
   // Based on experimentation, this is the minimal set of rights required for CreateProcessWithTokenW (contrary to current documentation).
   const DWORD dwTokenRights = TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID;
   ret = DuplicateTokenEx(hShellProcessToken, dwTokenRights, NULL, SecurityImpersonation, TokenPrimary, &hPrimaryToken);
   if (!ret)
   {
      dwLastErr = GetLastError();
      cout << L"Can't get primary token:  " << SysErrorMessageWithCode(dwLastErr);
      return false;
   }

   // Start the target process with the new token.
   ret = CreateProcessAsUserW(
      hPrimaryToken,
      szApp,
      szCmdLine,
      pSec,
      nullptr,
      0,
      0,
      NULL,
      szCurrDir,
      si,
      pi);
   if (!ret)
   {
      dwLastErr = GetLastError();
      cout << L"CreateProcessAsUserW failed:  " << SysErrorMessageWithCode(dwLastErr);
      return false;
   }

   return true;
}



BOOL IsElevated() {
   BOOL fRet = FALSE;
   HANDLE hToken = NULL;
   if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &hToken)) {
      TOKEN_ELEVATION Elevation;
      DWORD cbSize = sizeof(TOKEN_ELEVATION);
      if (GetTokenInformation(hToken, TokenElevation, &Elevation, sizeof(Elevation), &cbSize)) {
         fRet = Elevation.TokenIsElevated;
      }
   }
   if (hToken) {
      CloseHandle(hToken);
   }
   return fRet;
}

struct handle_data {
   unsigned long process_id;
   HWND window_handle;
};

BOOL CALLBACK enum_windows_callback(HWND handle, LPARAM lParam)
{
   handle_data& data = *(handle_data*)lParam;
   unsigned long process_id = 0;
   GetWindowThreadProcessId(handle, &process_id);
   if (data.process_id != process_id)
      return TRUE;
   data.window_handle = handle;
   return FALSE;
}

bool has_window(unsigned long process_id)
{
   handle_data data;
   data.process_id = process_id;
   data.window_handle = 0;
   EnumWindows(enum_windows_callback, (LPARAM)&data);
   return data.window_handle != nullptr;
}

int launch_game(char* appC, char* argC)
{
   std::string app(appC);
   std::string arg(argC);

   //Prepare CreateProcess args
   std::wstring app_w(app.length(), L' '); // Make room for characters
   std::copy(app.begin(), app.end(), app_w.begin()); // Copy string to wstring.

   std::wstring arg_w(arg.length(), L' '); // Make room for characters
   std::copy(arg.begin(), arg.end(), arg_w.begin()); // Copy string to wstring.

   std::wstring input = app_w + L" " + arg_w;
   wchar_t* arg_concat = const_cast<wchar_t*>(input.c_str());
   const wchar_t* app_const = app_w.c_str();

   TCHAR username[256];
   DWORD size = 256;
   if (!GetUserName((TCHAR*)username, &size))
   {
      std::cout << "GetUserName failed";
      return -1;
   }

   EXPLICIT_ACCESS pExplicitAccess;
   ZeroMemory(&pExplicitAccess, sizeof(pExplicitAccess));
   BuildExplicitAccessWithName(&pExplicitAccess, username, 0x001fffdf, GRANT_ACCESS, 0);

   PACL NewAcl;
   SetEntriesInAcl(1u, &pExplicitAccess, nullptr, &NewAcl);

   SECURITY_DESCRIPTOR secDes;
   ZeroMemory(&secDes, sizeof(secDes));
   if (!InitializeSecurityDescriptor(&secDes, 1u))
   {
      std::cout << "InitializeSecurityDescriptor failed";
      return -1;
   }

   if (!SetSecurityDescriptorDacl(&secDes, true, NewAcl, false))
   {
      std::cout << "SetSecurityDescriptorDacl failed";
      return -1;
   }

   STARTUPINFO si;
   PROCESS_INFORMATION pi;

   ZeroMemory(&si, sizeof(si));
   si.cb = sizeof(si);
   ZeroMemory(&pi, sizeof(pi));

   SECURITY_ATTRIBUTES pSec;
   ZeroMemory(&pSec, sizeof(pSec));
   pSec.nLength = sizeof(pSec);
   pSec.lpSecurityDescriptor = &secDes;
   pSec.bInheritHandle = false;

   if (!CreateProcess(nullptr, arg_concat, &pSec, nullptr, false, 0x20, nullptr, nullptr, &si, &pi))
   {
      std::cout << "CreateProcess failed";
      return -1;
   }
   

   while (!has_window(pi.dwProcessId))
   {
      Sleep(10);
   }

   PACL myAcl;
   auto gsi = GetSecurityInfo(GetCurrentProcess(), SE_KERNEL_OBJECT, DACL_SECURITY_INFORMATION, nullptr, nullptr, &myAcl, nullptr, nullptr);
   if (gsi != ERROR_SUCCESS)
   {
      std::cout << "GetSecurityInfo failed";
      return -1;
   }

   auto ssi = SetSecurityInfo(pi.hProcess, SE_KERNEL_OBJECT, DACL_SECURITY_INFORMATION | UNPROTECTED_DACL_SECURITY_INFORMATION, nullptr, nullptr, myAcl, nullptr);
   if (ssi != ERROR_SUCCESS)
   {
      std::cout << "SetSecurityInfo failed";
      return -1;
   }

   CloseHandle(pi.hProcess);
   CloseHandle(pi.hThread);

   return pi.dwProcessId;
}

int main(int argc, char* argv[])
{
   if (argc < 3)
   {
      std::cout << "needs game and arguments";
      return -1;
   }

   auto pid = launch_game(argv[1], argv[2]);

   std::cout << pid;

   return pid;
}