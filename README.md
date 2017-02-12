# WeMosDef
A WSDL-based C# API for Belkin WeMo devices.

Based on the WSDL definition from [https://github.com/sklose/WeMoWsdl](https://github.com/sklose/WeMoWsdl)

## Overview

I needed to automate my WeMo with a custom complex recipe, and IFTTT was not cutting it. After some unsuccessful attempts to control it via UPnP from Windows 10, I found that WSDL worked fine (with the annoyance that I have to tell it the IP address of my device). I wrote a wrapper around the WSDL actions to make things a little cleaner to consume.

The API supports the following methods:

GetState

On 

Off

GetSignalStrength

GetLogFileURL

GetIconURL

GetHomeId

GetFriendlyName

ChangeFriendlyName

## Example usage

Turn the device on:

```c#
var client = new WeMosDef.Client("192.168.15.11", 49153);
client.On();
```

## Meta

D'Arcy Rittich – @darcyrittich – drittich@gmail.com

Distributed under the MIT license. See LICENSE for more information.

https://github.com/drittich/WeMosDef
