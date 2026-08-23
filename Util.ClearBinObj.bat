FOR /f "tokens=*" %%a in ('dir bin.* /A:D /B /S') DO RMDIR /S /Q %%a
FOR /f "tokens=*" %%a in ('dir obj.* /A:D /B /S') DO RMDIR /S /Q %%a