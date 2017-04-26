// Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { default as Axios, AxiosResponse } from 'axios';

import * as ConstVariables from "./ConstVariables/commonVariables";

export class DfmHttpClient {
    private static urlPrefix = "http://localhost:";

    static async sendPostRequestAsync(port: string, command: string, content = null, workspacePath = null, relativePath = null, shouldSeparateMarkupResult = false): Promise<AxiosResponse> {
        let promise = Axios.post(this.urlPrefix + port, {
            name: command,
            markdownContent: content,
            workspacePath: workspacePath,
            relativePath: relativePath,
            shouldSeparateMarkupResult: shouldSeparateMarkupResult
        });

        let response: AxiosResponse;
        try {
            response = await promise;
        } catch (err) {
            let record = err.response;
            if (!record) {
                throw new Error(ConstVariables.noServiceErrorMessage);
            }

            switch (record.status) {
                case 400:
                    throw new Error(`[Client Error]: ${record.statusText}`);
                case 500:
                    throw new Error(`[Server Error]: ${record.statusText}`);
                default:
                    throw new Error(err);
            }
        }
        return response;
    }
}