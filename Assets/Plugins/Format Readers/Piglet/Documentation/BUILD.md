# Build Notes

* The main input file for building the manual is `index.md`, which is written in Pandoc-flavored Markdown.
* Run `./build.sh` to run `pandoc` on `index.md` and generate `index.html`.
* `build.sh` requires `stack`, `pandoc-crossref`, and `pandoc`. I installed `pandoc` and `pandoc-crossref` by cloning their git repos, checking out the appropriate git tag, and running `stack install`.
* `stack install` generally takes a long time to run (hours!) because it compiles a lot of things from source (e.g. GHC), but otherwise the installation process under WSL was painless.
* `pandoc-crossref` is very picky about which version of `pandoc` it is run with, which is why I had to install `pandoc` from its git repo rather than with `apt install`.

# Software Versions

```sh
$ pandoc --version
pandoc 2.11.2
Compiled with pandoc-types 1.22, texmath 0.12.0.3, skylighting 0.10.0.3,
citeproc 0.2, ipynb 0.1
User data directory: /home/benv/.local/share/pandoc or /home/benv/.pandoc
Copyright (C) 2006-2020 John MacFarlane. Web:  https://pandoc.org
This is free software; see the source for copying conditions. There is no
warranty, not even for merchantability or fitness for a particular purpose.sh
```

```sh
$ pandoc-crossref --version
pandoc-crossref v0.3.8.4 git commit ec2d4bc2eeaac30686147d0549abdd6e4af0776d (HEAD) built with Pandoc v2.11.2, pandoc-types v1.22 and GHC 8.8.4
```

