// **********************************************************************
//
// Copyright (c) 2002
// ZeroC, Inc.
// Huntsville, AL, USA
//
// All Rights Reserved
//
// **********************************************************************

#ifndef TEST_ICE
#define TEST_ICE

interface Test
{
    void deactivate();
};

local class Cookie
{
    nonmutating string message();
};

#endif
