# ciderpress2.com Web Site #

This is the content for the CiderPress II web site (https://ciderpress2.com/),
which provides documentation for the project.  Most of the files exist in two
places, `docs` and `ndocs`.

The `docs` directory is where github serves web pages from.  The contents
should always match up with the current release.

The `ndocs` directory is where changes are made during development.  The
files here provide documentation for the tip-of-tree code, which may have
features not yet present in the main release build.  This directory
also has scripts that help manage updates, and "include" files for common
elements, such as masthead, top nav bar, side nav bar, and footer.  These
elements are inserted by a Python script (`block-repl.py`) that must be
run whenever one of the "-incl" files changes.  This can be done manually,
to review the changes, or automatically, during publication.

When a software update is ready for release, the documents are published to
the `docs` directory by invoking the `publish.py` script.  In addition, a
handful of top-level documents (like the top-level README and installation
instructions) are also copied.

Some text variable substitutions can be performed on HTML and Markdown files
as they are copied, e.g. all occurrences of `${VERSION}` will be replaced with
the current app version.  This is used to update the links in the download
instructions (`Install.md`) to point at the current release.

The contents of files in the `ndocs` directory can be previewed by using the
[htmlpreview viewer](https://htmlpreview.github.io/?https://github.com/fadden/ciderpress2/blob/master/ndocs/index.html),
but be aware that some of the navigation links may not work.