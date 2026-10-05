# BioAnalyzer


## Genterating Helm Chart For App Host


````
cd src/BioAnalyzer/BioAnalyzer.AppHost
aspirate generate --skip-build --output-format helm
````


## Manual Index Setup
bio-docs

PmcId - string (R, F, Sort, Search)
Doi - string (R, Search)
Title - string (R, F, Search)
PageText - string (R, Search)
PageNumber - int (R, F)
FileName - string (R, F, Search)
Summary - string (R, Search)
Vector - Single Collection, Searchable, 1536, KNN
