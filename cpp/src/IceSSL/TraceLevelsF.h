// **********************************************************************
//
// Copyright (c) 2002
// ZeroC, Inc.
// Huntsville, AL, USA
//
// All Rights Reserved
//
// **********************************************************************

#ifndef ICE_SSL_TRACE_LEVELS_F_H
#define ICE_SSL_TRACE_LEVELS_F_H

#include <Ice/Handle.h>

namespace IceSSL
{

class TraceLevels;
typedef IceInternal::Handle<TraceLevels> TraceLevelsPtr;

}

namespace IceInternal
{

void incRef(IceSSL::TraceLevels*);
void decRef(IceSSL::TraceLevels*);

}

#endif
