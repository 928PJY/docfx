// Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { ExtensionContext, Uri, window, workspace } from "vscode";
import * as path from "path";
import * as fs from "fs";

import * as ConstVariables from "../ConstVariables/commonVariables";

export class TempPreviewFileProcessor {
    private _context: ExtensionContext;

    constructor(context: ExtensionContext) {
        this._context = context;
    }

    public writeTempPreviewFile(uri: Uri, config) {
        let environmentVariables = this.initializePath(config);
        this.writeTempPreviewFileCore(environmentVariables);
    }

    private writeTempPreviewFileCore(environmentVariables){

    }

    private initializePath(config) {
        let workspacePath = workspace.rootPath;
        let editor = window.activeTextEditor;
        let doc = editor.document;
        let fileName = doc.fileName;
        let rootPathLength = workspacePath.length;
        let relativePath = fileName.substr(rootPathLength + 1, fileName.length - rootPathLength);

        let filename = path.basename(relativePath);
        let filenameWithoutExt = filename.substr(0, filename.length - path.extname(relativePath).length);
        let builtHtmlPath = path.join(workspacePath, config.outputFolder, config.buildOutputSubFolder, path.dirname(relativePath).substring(config["buildSourceFolder"].length), filenameWithoutExt + ".html");
        let docfxPreviewFilePath = ConstVariables.filePathPrefix + path.join(workspacePath, config["outputFolder"], config["buildOutputSubFolder"], path.dirname(relativePath).substring(config["buildSourceFolder"].length), ConstVariables.docfxTempPreviewFile);

        if (!fs.existsSync(builtHtmlPath)) {
        }

        let pageRefreshJsFilePath = this._context.asAbsolutePath(path.join("media", "js", "htmlUpdate.js"));
        return {
            "builtHtmlPath": builtHtmlPath,
            "docfxPreviewFilePath": docfxPreviewFilePath,
            "pageRefreshJsFilePath": pageRefreshJsFilePath
        }
    }
}