:: DocFX can be installed using Chocolatey. You must do this manually before
:: running this script.

:: > cinst docfx

:: This command builds all the documentation based on the checked-in source code
:: and the files under /docfx. The output is saved to /docs.
docfx -f

:: If you want to build and host the site locally to examine the changes, use
:: the --serve argument, and DocFX will host the page on localhost:8080.
:: > docfx -f --serve