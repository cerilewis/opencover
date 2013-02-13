### Downloads
It seems that GitHub no longer wish to offer [downloads](https://github.com/blog/1302-goodbye-uploads) as part of the source hosting facility I have therefore decided to host the downloads on the opencover mirror site on [bitbucket](https://bitbucket.org/shaunwilde/opencover/downloads).

Alternatively why not try [nuget](http://nuget.org/packages/opencover).

### Licence
All Original Software is licensed under the [MIT Licence](https://github.com/sawilde/opencover/blob/master/License.md) and does not apply to any other 3rd party tools, utilities or code which may be used to develop this application.

If anyone is aware of any licence violations that this code may be making please inform the developers so that the issue can be investigated and rectified.

### Building
You will need:

1. Visual Studio VS2012 with C# and C++
2. WiX 3.7 (http://wix.codeplex.com/releases/view/99514)

All other software should be included with this repository. 

Nant scripts (encapsulated by the build.bat file) are used to build the project outside visual studio and will run all unit tests.

To build the code in 32-bit Debug mode just run Build in the root of the project folder.

To build a release package including installer, zip and nuget packages use 

> build create-release

### WIKI

Please review the [wiki pages](https://github.com/sawilde/opencover/wiki/_pages) on how to use OpenCover.

### Reports

For viewing the output from OpenCover [start here.](https://github.com/sawilde/opencover/wiki/Reports)

### Latest Drop as ZIP

No Git? Don't worry you can download the latest code as a [zip file](http://github.com/sawilde/opencover/zipball/master).

### Issues
Please raise issues on GitHub, if you can repeat the issue then please provide a sample to make it easier for us to also repeat it and then implement a fix. Please do not hijack unrelated issues, I would rather you create a new issue than add noise to an unrelated issue.

Dropbox is very useful for sharing files [Dropbox](http://db.tt/VanqFDn)

### Project Management
Was using AgileZen (which I quite liked) but the maintenance of an online board (for 1 person, me) vs the card wall (in office) got monotonous for no real gain.

### Thanks
I would like to thank the guys at CodeBetter, Devlicious and Los Techies who arranged my MSDN licence and also to the NextGenUG and their free swag from where I got lots of useful tools. I'd also like to thank my employers, colleagues and friends for all their support. 
