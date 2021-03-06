#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

#this is CMakeLists.txt for ble_hl_unittests
cmake_minimum_required(VERSION 2.8.11)

compileAsC99()
set(theseTestsName ble_hl_unittests)

set(${theseTestsName}_cpp_files
    ${theseTestsName}.cpp
)

set(ble_hl_test_sources
    ../../src/ble_utils.c
    ../../src/ble_hl.c
)

if(LINUX)
    # Include GIO headers/libs
    include_directories(${GIOUNIX_INCLUDE_DIRS})
    set(LIBS ${GIOUNIX_LIBRARIES})
endif()

set(ble_hl_test_headers
    ../../inc/ble_utils.h
    ../../inc/ble_hl.h
)

include_directories(
    ../../inc
    ${GW_SRC}
    ${GW_INC}
)

set(${theseTestsName}_c_files
    ${ble_hl_test_sources}
)

set(${theseTestsName}_h_files
    ${ble_hl_test_headers}
)

build_test_artifacts(${theseTestsName} ON ${LIBS})
