#!/bin/bash
# Setup binder for Redroid on Linux. Run with sudo or as root.
# Redroid requires /dev/binder, /dev/hwbinder, /dev/vndbinder.
set -e

echo "Installing linux-modules-extra (includes binder_linux)..."
apt-get update -qq
apt-get install -y linux-modules-extra-$(uname -r)

echo "Loading binder module..."
modprobe binder_linux devices="binder,hwbinder,vndbinder"

echo "Mounting binderfs..."
mkdir -p /dev/binderfs
mount -t binder binder /dev/binderfs 2>/dev/null || true

echo "Creating symlinks..."
ln -sf /dev/binderfs/binder /dev/binder
ln -sf /dev/binderfs/hwbinder /dev/hwbinder
ln -sf /dev/binderfs/vndbinder /dev/vndbinder

echo "Verifying..."
ls -la /dev/binder /dev/hwbinder /dev/vndbinder
ls /dev/binderfs/

echo "Binder setup complete."
