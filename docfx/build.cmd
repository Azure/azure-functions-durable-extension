:: DocFX can be installed using Chocolatey. You must do this manually before
:: running this script.

:: > cinst docfx

:: This command builds all the documentation based on the checked-in source code
:: and the files under /docfx. The output is saved to /docs.
docfx docfx.json -f -t _exported_templates\default

:: If you want to build and host the site locally to examine the changes, append
:: the --serve argument to the arg list above. DocFX will host the page on localhost:8080.
:: > docfx docfx.json -f -t _exported_templates\default --serve