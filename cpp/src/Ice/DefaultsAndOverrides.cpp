// **********************************************************************
//
// Copyright (c) 2001
// ZeroC, Inc.
// Huntsville, AL, USA
//
// All Rights Reserved
//
// **********************************************************************

#include <Ice/DefaultsAndOverrides.h>
#include <Ice/Properties.h>
#include <Ice/Network.h>

using namespace std;
using namespace Ice;
using namespace IceInternal;

void IceInternal::incRef(DefaultsAndOverrides* p) { p->__incRef(); }
void IceInternal::decRef(DefaultsAndOverrides* p) { p->__decRef(); }

IceInternal::DefaultsAndOverrides::DefaultsAndOverrides(const PropertiesPtr& properties) :
    overrideTimeout(false),
    overrideTimeoutValue(-1),
    overrideComppress(false),
    overrideComppressValue(false)
{
    const_cast<string&>(defaultProtocol) = properties->getPropertyWithDefault("Ice.Default.Protocol", "tcp");

    const_cast<string&>(defaultHost) = properties->getProperty("Ice.Default.Host");
    if(defaultHost.empty())
    {
	const_cast<string&>(defaultHost) = getLocalHost(true);
    }

    const_cast<string&>(defaultRouter) = properties->getProperty("Ice.Default.Router");

    string value;
    
    value = properties->getProperty("Ice.Override.Timeout");
    if(!value.empty())
    {
	const_cast<bool&>(overrideTimeout) = true;
	const_cast<Int&>(overrideTimeoutValue) = properties->getPropertyAsInt("Ice.Override.Timeout");
    }

    value = properties->getProperty("Ice.Override.Compress");
    if(!value.empty())
    {
	const_cast<bool&>(overrideComppress) = true;
	const_cast<bool&>(overrideComppressValue) = properties->getPropertyAsInt("Ice.Override.Compress");
    }

    const_cast<string&>(defaultLocator) = properties->getProperty("Ice.Default.Locator");
}
