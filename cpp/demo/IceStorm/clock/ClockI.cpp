// **********************************************************************
//
// Copyright (c) 2001
// ZeroC, Inc.
// Huntsville, AL, USA
//
// All Rights Reserved
//
// **********************************************************************

#include <Ice/Ice.h>

#include <ClockI.h>

using namespace std;

void
ClockI::tick(const Ice::Current&)
{
    cout << "tick" << endl;
}
