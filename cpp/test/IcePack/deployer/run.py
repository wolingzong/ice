#!/usr/bin/env python
# **********************************************************************
#
# Copyright (c) 2002
# ZeroC, Inc.
# Billerica, MA, USA
#
# All Rights Reserved.
#
# Ice is free software; you can redistribute it and/or modify it under
# the terms of the GNU General Public License version 2 as published by
# the Free Software Foundation.
#
# **********************************************************************

import os, sys

for toplevel in [".", "..", "../..", "../../..", "../../../.."]:
    toplevel = os.path.normpath(toplevel)
    if os.path.exists(os.path.join(toplevel, "config", "TestUtil.py")):
        break
else:
    raise "can't find toplevel directory!"

sys.path.append(os.path.join(toplevel, "config"))
import TestUtil
import IcePackAdmin

testdir = os.path.join(toplevel, "test", "IcePack", "deployer")

os.environ['LD_LIBRARY_PATH'] = testdir + ":" + os.environ['LD_LIBRARY_PATH']

#
# Start the client.
#
def startClient(options):

    updatedClientOptions = TestUtil.clientOptions.replace("TOPLEVELDIR", toplevel) + \
                           " --Ice.Default.Locator=\"IcePack/Locator:default -p 12346\" " + \
                           options

    print "starting client...",
    clientPipe = os.popen(os.path.join(testdir, "client") + updatedClientOptions)
    print "ok"

    for output in clientPipe.xreadlines():
        print output,
    
    clientStatus = clientPipe.close()
    if clientStatus:
        print "failed"

#
# Start IcePack.
#
IcePackAdmin.cleanDbDir(os.path.join(testdir, "db"))

icePackRegistryPipe = IcePackAdmin.startIcePackRegistry(toplevel, "12346", testdir)
icePackNodePipe = IcePackAdmin.startIcePackNode(toplevel, testdir)

#
# Deploy the application, run the client and remove the application.
#
print "deploying application...",
IcePackAdmin.addApplication(toplevel, os.path.join(testdir, "application.xml"), "");
print "ok"

startClient("")

print "removing application...",
IcePackAdmin.removeApplication(toplevel, os.path.join(testdir, "application.xml"));
print "ok"

#
# Deploy the application with some targets to test targets, run the
# client to test targets (-t options) and remove the application.
#
print "deploying application with target...",
IcePackAdmin.addApplication(toplevel, os.path.join(testdir, "application.xml"), "debug localnode.Server1.manual")
print "ok"

startClient("-t")

print "removing application...",
IcePackAdmin.removeApplication(toplevel, os.path.join(testdir, "application.xml"));
print "ok"

#
# Shutdown IcePack.
#
IcePackAdmin.shutdownIcePackNode(toplevel, icePackNodePipe)
IcePackAdmin.shutdownIcePackRegistry(toplevel, icePackRegistryPipe)

sys.exit(0)
