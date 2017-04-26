// Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { Uri, window, workspace } from "vscode";
import * as fs from "fs";
import * as path from "path";

import * as ConstVariables from "../ConstVariables/commonVariables";
import { PreviewProcessor } from "./previewProcessor";
import { ProxyResponse } from "../Proxy/proxyResponse";
import { TempPreviewFileProcessor } from "../Utility/tempPreviewFileProcessor";

export class DocFXPreviewProcessor extends PreviewProcessor {
    public isMarkdownFileChanged: boolean = false;
    public markupResult: string;

    private tempPreviewFileProcessor: TempPreviewFileProcessor;

    constructor(context) {
        super(context);
        this.tempPreviewFileProcessor = new TempPreviewFileProcessor(context);
    }

    public startPreview(uri: Uri, callback) {
        if (this.validEnvironment()) {
            let tempPreviewFilePath = this.tempPreviewFileProcessor.writeTempPreviewFile(uri, this.parseConfig());
            callback(tempPreviewFilePath);
            this.updateContent(uri);
        } else {
            window.showErrorMessage(`[Exntension Error]: Please Open a DocFX project folder`);
        }
    }

    private validEnvironment() {
        let workspacePath = workspace.rootPath;
        if (!workspacePath) {
            return false;
        } else {
            let previewConfigFilePath = path.join(workspacePath, ConstVariables.previewConfigFileName);
            let docfxConfigFileName = ConstVariables.docfxConfigFileName;
            if (fs.existsSync(previewConfigFilePath)) {
                let previewconfig = fs.readFileSync(previewConfigFilePath)
                if (previewconfig["docfxConfigFileName"] != null) {
                    docfxConfigFileName = previewconfig["docfxConfigFileName"];
                }
            }
            return fs.existsSync(path.join(workspacePath, docfxConfigFileName));
        }
    }

    private parseConfig() {
        // TODO: make this configable
        return {
            "outputFolder": "_site",
            "buildSourceFolder": "articles",
            "buildOutputSubFolder": "articles",
            "reference": {
                "link": "href",
                "script": "src",
                "img": "src"
            }
        };
    }

    protected pageRefresh(response: ProxyResponse){
        this.isMarkdownFileChanged = true;
        this.markupResult = response.markupResult;
    }
}