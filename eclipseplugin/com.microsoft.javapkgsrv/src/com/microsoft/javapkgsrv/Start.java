// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

package com.microsoft.javapkgsrv;

import java.io.IOException;

import org.eclipse.equinox.app.IApplication;
import org.eclipse.equinox.app.IApplicationContext;

public class Start implements IApplication {

    @Override
    public Object start(IApplicationContext context) throws Exception {
        String[] args = (String[]) context.getArguments().get("application.args");
        System.out.println("Arguments:");
        for (String arg : args) {
            System.out.println("\t" + arg);
        }

        ClientProxy proxy;
        if (args.length == 1) {
            proxy = new ClientProxy(args[0]);
        } else {
            proxy = new ClientProxy();
        }

        try {
            proxy.Run();
        } catch (IOException e) {
            e.printStackTrace();
        }

        return null;
    }

    @Override
    public void stop() {
    }

}
