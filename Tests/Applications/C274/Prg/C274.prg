// 274. error XS0121: The call is ambiguous between the following methods or properties: 
// 'Functions.Test(int, int)' and 'Functions.Test(dword, dword)'
// In vulcan this compiles without errors, no matter the state of /vo4
FUNCTION Test(n AS INT,m AS INT) AS VOID
FUNCTION Test(n AS DWORD,m AS DWORD) AS VOID
FUNCTION Start() AS VOID
LOCAL d :=0 AS DWORD
Test(1,d-2)

