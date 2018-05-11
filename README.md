#Introduction 
Cake build scripts for Xamarin / C# / MvvmCross / Whatever projects

Author: Jacob Duijzer

## NOTES

mbp-jacobd:THTForms jacob.duijzer$ sh ~/Documents/Projecten/Appcell/Redhotminute.Appollo.Cake.BuildScripts/build.sh -s ~/Documents/Projecten/Appcell/Redhotminute.Appollo.Cake.BuildScripts/build.cake --target=Debug

bp-jacobd:MonkeyTown jacob.duijzer$ sh ~/Documents/Projecten/Appcell/Redhotminute.Appollo.Cake.BuildScripts/build.sh -s ~/Documents/Projecten/Appcell/Redhotminute.Appollo.Cake.BuildScripts/build.cake --target=NugetRestore --workingdirectory=/Users/jacob.duijzer/Documents/Projecten/JacobsBitBuckets/MonkeyTown/


https://github.com/OmniSharp/csharp-language-server-protocol/blob/master/build.cake

https://github.com/jzeferino/cake-xamarin-sample/blob/master/build.cake


https://ghuntley.com/blog/example-of-xamarin-ios-with-cake

https://github.com/cake-build/cake/issues/1629

# Good Xamarin examples

https://github.com/ghuntley/appstore-automation-with-fastlane/blob/master/build.cake

## Nice

## iOS TEST BUILD & UPLOAD
./build.sh --target=AppCenterRelease-iOS --configuration=Debug --appcenter_owner=CakeTestApp --appcenter_appname=CakeTestApp-Dev --appcenter_distributiongroup=Collaborator

## ANDROID TEST BUILD & UPLOAD
./build.sh --target=AppCenterRelease-Droid --configuration=Debug --appcenter_owner=CakeTestApp --appcenter_appname=CakeTestApp-Dev-1 --appcenter_distributiongroup=Collaborator