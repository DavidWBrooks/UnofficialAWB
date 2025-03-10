# UnofficialAWB
Private builds of recent AutoWikiBrowser sources. I will update this on occasion in the hope it is useful. See the release notes for details of the revision number used in the latest release.

# Installation
1. Navigate to [releases/latest](https://github.com/DavidWBrooks/UnofficialAWB/releases/latest), or you can use the "latest" button to the right.
1. Download the appropriate AutoWikiBrowser*.zip file from there. The release note identifies the AutoWikiBrowser version (usually of the next official release). Files with the suffix "-ARM" are specifically built to run on an ARM64 processor.
1. Follow the instructions on the [AutoWikiBrowser project page](https://en.wikipedia.org/wiki/Wikipedia:AutoWikiBrowser), starting at the appropriate "Running on..." section.

# FixResx
This repo also contains a command-line utility to fix a resource file whose geometry was broken by the Visual Studio process. It gets numbers from a legacy designer file and merges them into the VS-created resource file. See the source for more information. It needs some updates but the first version seems to do the work.