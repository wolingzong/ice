// **********************************************************************
//
// Copyright (c) 2002
// ZeroC, Inc.
// Huntsville, AL, USA
//
// All Rights Reserved
//
// **********************************************************************

#include <Ice/Application.h>
#include <ServantLocatorI.h>
#include <TestCommon.h>

using namespace std;
using namespace Ice;

class TestServer : public Application
{
public:

    virtual int run(int, char*[]);
};

int
main(int argc, char* argv[])
{
    TestServer app;
    return app.main(argc, argv);
}

int
TestServer::run(int argc, char* argv[])
{
    communicator()->getProperties()->setProperty("TestAdapter.Endpoints", "default -p 12345 -t 2000");
    Ice::ObjectAdapterPtr adapter = communicator()->createObjectAdapter("TestAdapter");
    ServantLocatorPtr locator = new ServantLocatorI;
    adapter->addServantLocator(locator, "");
    adapter->activate();
    adapter->waitForDeactivate();
    return EXIT_SUCCESS;
}
