# 📦 Iniciar Repositorio Git

Esta carpeta está lista para crear un repositorio Git independiente.

## 1️⃣ Crear Repositorio Local

```bash
cd BluetoothBridge

git init
git config user.name "Tu Nombre"
git config user.email "tu.email@example.com"

git add .
git commit -m "Initial commit: Bluetooth Bridge Windows"
```

## 2️⃣ Crear Repositorio en GitHub

1. Inicia sesión en [github.com](https://github.com)
2. Click "New"
3. **Name:** `BluetoothBridge` (o similar)
4. **Description:** "Puente Bluetooth a WebSocket para Sistema de Estabilometría"
5. Public/Private
6. NO selecciones "Initialize this repository"
7. Click "Create repository"

## 3️⃣ Conectar al Remoto

```bash
git remote add origin https://github.com/TU_USUARIO/BluetoothBridge.git
git branch -M main
git push -u origin main
```

## ✅ Resultado

Repositorio creado con estructura lista para producción.

---

**Comandos útiles:**
```bash
git status      # Estado actual
git log         # Historial
git push        # Subir cambios
git pull        # Descargar cambios
```
