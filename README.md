fitbitdotnet
============

FitBit Library for .NET Applications, Written in C# to support the Fitbit Ultra.

Note! This library is radically incomplete. If you're looking for something to develop
a production application with, please check out more complete projects like libfitbit.

To go along with the incompleteness, please excuse the lack of code structure in some
places. This is very much a work in progress and has a great deal of time and effort
left to go before it is even remotely useful.


Use the code template below to see what data we can get. (There are fields in the
tracker object as well)

    var bs = new BaseStationUSB(false);    
    var tracker = new Tracker(); 
    var someData = tracker.DumpData();



(News and Updates moved to the wiki)