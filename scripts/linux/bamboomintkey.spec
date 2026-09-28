# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# RPM spec cho Fedora (build bằng rpmbuild). Xem scripts/package_linux.sh.

Name:           bamboomintkey
Version:        1.1.0
Release:        1%{?dist}
Summary:        Vietnamese Telex Input Method for Fcitx5
License:        MIT
URL:            https://github.com/thatislg/BambooMintKey

# Addon là binary x86_64 (NativeAOT), không phải noarch.
Requires:       fcitx5, dotnet-runtime-10.0

%description
BambooMintKey là bộ gõ tiếng Việt (Telex) hiện đại cho Fcitx5 trên Linux,
tích hợp từ điển âm tiết MIT, thẩm định on-the-fly và tự hoàn tác tiếng Anh.

%prep
# Không cần build trong RPM; file đã được build sẵn ở bước trước (dotnet + cmake).

%install
mkdir -p %{buildroot}%{_libdir}/fcitx5 \
         %{buildroot}%{_datadir}/fcitx5/addon \
         %{buildroot}%{_datadir}/fcitx5/inputmethod \
         %{buildroot}%{_datadir}/icons/hicolor/scalable/apps \
         %{buildroot}%{_datadir}/applications \
         %{buildroot}%{_libdir}/bamboomintkey/ui \
         %{buildroot}%{_bindir}

# Core + addon (từ staging)
install -m 755 %{_sourcedir}/libbamboomintkey.so  %{buildroot}%{_libdir}/fcitx5/
install -m 755 %{_sourcedir}/BambooMintKeyCore.so  %{buildroot}%{_libdir}/fcitx5/
install -m 644 %{_sourcedir}/bamboomintkey-addon.conf %{buildroot}%{_datadir}/fcitx5/addon/bamboomintkey.conf
install -m 644 %{_sourcedir}/bamboomintkey.conf %{buildroot}%{_datadir}/fcitx5/inputmethod/bamboomintkey.conf
install -m 644 %{_sourcedir}/fcitx_bamboomintkey.svg %{buildroot}%{_datadir}/icons/hicolor/scalable/apps/
install -m 644 %{_sourcedir}/fcitx_bamboomintkey_e.svg %{buildroot}%{_datadir}/icons/hicolor/scalable/apps/
install -m 644 %{_sourcedir}/bamboomintkey.svg %{buildroot}%{_datadir}/icons/hicolor/scalable/apps/

# UI (framework-dependent) + launcher + desktop entry
cp -a %{_sourcedir}/ui/. %{buildroot}%{_libdir}/bamboomintkey/ui/
ln -sf %{_libdir}/bamboomintkey/ui/BambooMintKey.UI.Linux %{buildroot}%{_bindir}/bamboomintkey-ui
install -m 644 %{_sourcedir}/bamboomintkey-settings.desktop %{buildroot}%{_datadir}/applications/

%post
cat <<'MSG'
BambooMintKey đã được cài đặt thành công!

=== CÁC BƯỚC HOÀN TẤT (bắt buộc) ===
  1. Mở Fcitx5 Configuration:  fcitx5-configtool
  2. Thêm bộ gõ 'BambooMintKey' vào danh sách Input Method.
  3. Khởi động lại Fcitx5 hoặc đăng xuất/đăng nhập lại để kích hoạt.

Lưu ý: bảng cài đặt (bamboomintkey-ui) cần .NET 10 runtime.
  Nếu chưa cài:  sudo dnf install dotnet-runtime-10.0
MSG
touch --no-create %{_datadir}/icons/hicolor &>/dev/null || :
gtk-update-icon-cache %{_datadir}/icons/hicolor &>/dev/null || :
update-desktop-database %{_datadir}/applications &>/dev/null || :

%postun
touch --no-create %{_datadir}/icons/hicolor &>/dev/null || :
gtk-update-icon-cache %{_datadir}/icons/hicolor &>/dev/null || :
update-desktop-database %{_datadir}/applications &>/dev/null || :

%files
%{_libdir}/fcitx5/libbamboomintkey.so
%{_libdir}/fcitx5/BambooMintKeyCore.so
%{_datadir}/fcitx5/addon/bamboomintkey.conf
%{_datadir}/fcitx5/inputmethod/bamboomintkey.conf
%{_datadir}/icons/hicolor/scalable/apps/fcitx_bamboomintkey.svg
%{_datadir}/icons/hicolor/scalable/apps/fcitx_bamboomintkey_e.svg
%{_datadir}/icons/hicolor/scalable/apps/bamboomintkey.svg
%{_libdir}/bamboomintkey/ui/
%{_bindir}/bamboomintkey-ui
%{_datadir}/applications/bamboomintkey-settings.desktop

%changelog
* Sun Sep 27 2026 Dương Gia Long <thatislg@users.noreply.github.com> - 1.1.0-1
- Initial package
