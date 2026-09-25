cmake_minimum_required(VERSION 3.16)

project(bamboomintkey VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

find_package(Fcitx5Core REQUIRED)
find_package(Fcitx5Config REQUIRED)
find_package(Fcitx5Utils REQUIRED)

# Đường dẫn tới BambooMintKeyCore.so (do dotnet publish sinh ra).
# Truyền qua -DBAMBOOMINTKEY_CORE_SO=/path/to/BambooMintKeyCore.so
set(BAMBOOMINTKEY_CORE_SO "" CACHE FILEPATH "Path to BambooMintKeyCore.so")

add_library(bamboomintkey SHARED engine.cpp)

target_include_directories(bamboomintkey PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})

target_link_libraries(bamboomintkey PRIVATE
    Fcitx5::Core
    Fcitx5::Config
    Fcitx5::Utils
)

if(BAMBOOMINTKEY_CORE_SO)
    # Imported target để link trực tiếp tới file .so (tránh CMake chuyển thành -l).
    add_library(bamboomintkey_core SHARED IMPORTED)
    set_target_properties(bamboomintkey_core PROPERTIES
        IMPORTED_LOCATION "${BAMBOOMINTKEY_CORE_SO}")
    target_link_libraries(bamboomintkey PRIVATE bamboomintkey_core)
endif()

# rpath: tìm BambooMintKeyCore.so cùng thư mục với addon.
set_target_properties(bamboomintkey PROPERTIES
    INSTALL_RPATH "$ORIGIN"
    BUILD_RPATH "$ORIGIN"
)

# Sinh file conf từ template.
configure_file(bamboomintkey-addon.conf.in bamboomintkey-addon.conf @ONLY)
configure_file(bamboomintkey.conf.in bamboomintkey.conf @ONLY)

# Cài đặt.
include(GNUInstallDirs)
install(TARGETS bamboomintkey
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}/fcitx5)

install(FILES ${CMAKE_CURRENT_BINARY_DIR}/bamboomintkey-addon.conf
    DESTINATION ${CMAKE_INSTALL_DATADIR}/fcitx5/addon
    RENAME bamboomintkey.conf)

install(FILES ${CMAKE_CURRENT_BINARY_DIR}/bamboomintkey.conf
    DESTINATION ${CMAKE_INSTALL_DATADIR}/fcitx5/inputmethod)

# Cài icon cho input method (theo chuẩn Fcitx5 hicolor).
install(FILES "${CMAKE_CURRENT_SOURCE_DIR}/../media/bamboo_mint_key_ico.svg"
    DESTINATION ${CMAKE_INSTALL_DATADIR}/icons/hicolor/scalable/apps
    RENAME fcitx_bamboomintkey.svg)
install(FILES "${CMAKE_CURRENT_SOURCE_DIR}/../media/bamboo_mint_key_ico_e.svg"
    DESTINATION ${CMAKE_INSTALL_DATADIR}/icons/hicolor/scalable/apps
    RENAME fcitx_bamboomintkey_e.svg)

# Cài đặt BambooMintKeyCore.so cạnh addon để rpath $ORIGIN tìm thấy.
if(BAMBOOMINTKEY_CORE_SO)
    install(FILES "${BAMBOOMINTKEY_CORE_SO}"
        DESTINATION ${CMAKE_INSTALL_LIBDIR}/fcitx5)
endif()
