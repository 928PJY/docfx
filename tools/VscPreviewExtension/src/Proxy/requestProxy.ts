// Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { ExtensionContext, window } from "vscode";
import * as childProcess from "child_process";

import * as ConstVariables from "../ConstVariables/commonVariables";
import { Common } from "../common";
import { DfmService } from "../dfmService";
import { PreviewType } from "../ConstVariables/previewType";
import { ProxyRequest } from "./proxyRequest";
import { ProxyResponse } from "./proxyResponse";
import { RequestArray } from "./RequestArray";

export class requestProxy {
    private static _instance: requestProxy = new requestProxy();

    private _isChildProcessStarting: boolean = false;
    private _requestArray = new RequestArray();
    private _spawn: childProcess.ChildProcess;
    private _serverPort = "4002";

    public static getInstance(): requestProxy {
        return requestProxy._instance;
    }

    public newRequest(request: ProxyRequest) {
        request.storageChildProcessPid(this._spawn ? this._spawn.pid : 0);
        this._requestArray.push(request);
        this.requestProcess();
    }

    public async stopProxy() {
        try {
            await DfmService.exitAsync(this._serverPort);
        } catch (err) {
            window.showErrorMessage(`[Server Error]: ${err}`);
        }
    }

    private requestProcess() {
        let request;
        while ((request = this._requestArray.pop()) != null) {
            if (this._isChildProcessStarting) {
                this._requestArray.push(request);
                return;
            } else {
                this.requestProcessCore(request);
            }
        }
    }

    private async requestProcessCore(request: ProxyRequest) {
        try {
            let res;
            switch (request.previewType) {
                case PreviewType.dfmPreview:
                case PreviewType.docfxPreview:
                    res = await DfmService.previewAsync(this._serverPort, request.content, request.workspacePath, request.relativePath, request.shouldSeparateMarkupResult);
                    break;
                case PreviewType.tokenTreePreview:
                    res = await DfmService.getTokenTreeAsync(this._serverPort, request.content, request.workspacePath, request.relativePath);
                    break;
            }
            request.callback(null, new ProxyResponse(res ? res.data : "", request.relativePath, request.documentUri));
        } catch (err) {
            if (err.message == ConstVariables.noServiceErrorMessage) {
                this._requestArray.push(request);
                let currentPid = this._spawn ? this._spawn.pid : 0;
                if (currentPid === request.oldPid) {
                    this.newHttpServerAndStartPreview(request.context);
                } else {
                    this.requestProcess();
                }
            } else {
                request.callback(err);
            }
        }
    }

    private newHttpServerAndStartPreview(context: ExtensionContext) {
        if (this._isChildProcessStarting)
            return;
        this._isChildProcessStarting = true;
        window.showInformationMessage("Environment initializing, please wait for several seconds!");
        this.getFreePort(port => this.newHttpServerAndStartPreviewCore(port, context));
    }

    private getFreePort(callback) {
        let http = require("http");
        let server = http.createServer();
        server.listen(0);
        server.on('listening', function () {
            var port = server.address().port;
            server.close();
            callback(port);
        })
    }

    private newHttpServerAndStartPreviewCore(port, context: ExtensionContext) {
        let that = this;
        this._serverPort = port.toString();
        let exePath = context.asAbsolutePath("./DfmHttpService/DfmHttpService.exe");
        try {
            this._spawn = Common.spawn(exePath + " " + this._serverPort, {});
        }
        catch (err) {
            window.showErrorMessage(`[Extension Error]: ${err}`);
        }
        if (!this._spawn.pid) {
            window.showErrorMessage(`[Child process Error]: DfmProcess lost!`);
            return;
        }
        this._spawn.stdout.on("data", function (data) {
            that._isChildProcessStarting = false;
            that.requestProcess();
        });
        this._spawn.stderr.on("data", function (data) {
            window.showErrorMessage(`[Child process Error]: ${data.toString()}`);
        });
    }
}
