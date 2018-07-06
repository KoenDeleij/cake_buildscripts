#Redhotminute.Appollo.Cake.BuildScripts

Cake build scripts for Xamarin / C# / MvvmCross / Whatever projects you want to build

Author: Jacob Duijzer

## TL;DR
- TFS Repo: http://rhm-p-tfs01.kantoor.tld:8080/tfs/RedHotMinute/Apps/_git/Redhotminute.Appollo.Cake.BuildScripts
- Nexus: http://rhm-p-repo01.kantoor.tld:8081/repository/raw-rhm-mobile/cake/cakebuild-latest.zip

## How it works (basically)
Voor het builden haalt het script de laatste versie van de Cake scripts op en pakt deze uit. Daarna kan je Cake builds starten. In de log zie je de output van de settings, deze spreken voor zich en zijn volgens mij duidelijk genoeg om te achterhalen wat je nog mist.




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
./build.sh --target=AppCenterRelease-iOS --configuration=Debug --appcenter_owner=CakeTestApp --appcenter_appname=CakeTestApp-Dev --appcenter_distributiongroup=Collaborators

## ANDROID TEST BUILD & UPLOAD
./build.sh --target=AppCenterRelease-Droid --configuration=Debug --appcenter_owner=CakeTestApp --appcenter_appname=CakeTestApp-Dev-1 --appcenter_distributiongroup=Collaborators

 ./build.sh --target=Build-Droid --configuration=Debug --android_keystorefile=CakeTestApp/keystore/CakeTestApp.keystore --android_keystorealias=CakeTestApp --android_keystorepasswd=2:Tweerondjes
 
 ./build.sh --target=AppCenterRelease-Droid --configuration=Debug --android_keystorefile=/Users/jacob.duijzer/Documents/Projecten/Appcell/Redhotminute.Appollo.Cake.BuildScripts/CakeTestApp/keystore/CakeTestApp.keystore --android_keystorealias=CakeTestApp --android_keystorepasswd=2:Tweerondjes --appcenter_owner=CakeTestApp --appcenter_appname=CakeTestApp-Dev-1 --appcenter_distributiongroup=Collaborators