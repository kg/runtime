// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

#include <stdlib.h>
#include <stdio.h>
#include "pal_icushim_internal.h"
#include "pal_icushim.h"
#include <unicode/putil.h>
#include <unicode/uversion.h>

#ifdef __EMSCRIPTEN__
#include <emscripten.h>

EMSCRIPTEN_KEEPALIVE int32_t mono_wasm_load_icu_data (void * pData);

EMSCRIPTEN_KEEPALIVE int32_t mono_wasm_load_icu_data (void * pData) {
    UErrorCode status = 0;
    udata_setCommonData (pData, &status);

    if (U_FAILURE(status)) {
        EM_ASM({
            console.debug("udata_setCommonData failed with error", $0);
        }, status);
        return 0;
    } else {
        EM_ASM({
            console.debug("udata_setCommonData ok");
        });
        return 1;
    }
}
#endif

int32_t GlobalizationNative_LoadICU(void)
{
    EM_ASM(
        console.debug("static LoadICU");
    );

    const char* icudir = getenv("DOTNET_ICU_DIR");
    if (icudir) {
        EM_ASM(
            console.debug("static LoadICU setting data directory");
        );
        u_setDataDirectory(icudir);
    } else {
        EM_ASM(
            console.debug("static LoadICU not setting data directory");
        );
        // default ICU search path behavior will be used, see http://userguide.icu-project.org/icudata
    }

    // we can also use `udata_setCommonData(const void *data, UErrorCode *err)` API here

    UErrorCode status = 0;
    u_init(&status);

    if (U_FAILURE(status)) {
        EM_ASM({
            console.debug("u_init failed with error", $0);
        }, status);
        return 0;
    } else {
        EM_ASM(
            console.debug("u_init ok");
        );
    }

    return 1;
}

void GlobalizationNative_InitICUFunctions(void* icuuc, void* icuin, const char* version, const char* suffix)
{
    // no-op for static
}

int32_t GlobalizationNative_GetICUVersion(void)
{
    UVersionInfo versionInfo;
    u_getVersion(versionInfo);

    return (versionInfo[0] << 24) + (versionInfo[1] << 16) + (versionInfo[2] << 8) + versionInfo[3];
}
