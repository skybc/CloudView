using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace CloudView.Controls;

public partial class PointCloudViewer
{
    #region OpenGL 初始化和清理

    internal void InitializeOpenGL(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _hDC = Win32Interop.GetDC(hwnd);
        if (_hDC == IntPtr.Zero)
            throw new Exception("Failed to get device context");

        var pfd = new Win32Interop.PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Marshal.SizeOf<Win32Interop.PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = Win32Interop.PFD_DRAW_TO_WINDOW | Win32Interop.PFD_SUPPORT_OPENGL | Win32Interop.PFD_DOUBLEBUFFER,
            iPixelType = Win32Interop.PFD_TYPE_RGBA,
            cColorBits = 32,
            cDepthBits = 24,
            cStencilBits = 8,
            iLayerType = Win32Interop.PFD_MAIN_PLANE
        };

        int pixelFormat = Win32Interop.ChoosePixelFormat(_hDC, ref pfd);
        if (pixelFormat == 0)
            throw new Exception("Failed to choose pixel format");

        if (!Win32Interop.SetPixelFormat(_hDC, pixelFormat, ref pfd))
            throw new Exception("Failed to set pixel format");

        _hGLRC = Win32Interop.wglCreateContext(_hDC);
        if (_hGLRC == IntPtr.Zero)
            throw new Exception("Failed to create OpenGL context");

        if (!Win32Interop.wglMakeCurrent(_hDC, _hGLRC))
            throw new Exception("Failed to make OpenGL context current");

        _gl = GL.GetApi(GetProcAddressFunc);

        InitializeShaders();
        InitializeTextShaders();
        InitializeBuffers();
        CreateCoordinateAxisAndGrid();

        _isInitialized = true;
        if (Points != null && Points.Count > 0)
        {
            UpdatePointCloudBuffer();
        }

        if ((_sharpNeedsRebuild && Shapes != null) || (Shapes != null && Shapes.Count > 0))
        {
            UpdateShapesBuffers();
        }
    }

    private nint GetProcAddressFunc(string name)
    {
        var addr = Win32Interop.wglGetProcAddress(name);
        if (addr != IntPtr.Zero)
            return addr;

        if (_opengl32Handle == IntPtr.Zero)
        {
            _opengl32Handle = Win32Interop.LoadLibrary("opengl32.dll");
        }
        return Win32Interop.GetProcAddress(_opengl32Handle, name);
    }

    internal void CleanupOpenGL()
    {
        if (_gl != null && _isInitialized)
        {
            if (_hDC != IntPtr.Zero && _hGLRC != IntPtr.Zero)
            {
                Win32Interop.wglMakeCurrent(_hDC, _hGLRC);
            }

            if (_vao != 0)
            {
                _gl.DeleteVertexArray(_vao);
                _vao = 0;
            }
            if (_vbo != 0)
            {
                _gl.DeleteBuffer(_vbo);
                _vbo = 0;
            }

            if (_shaderProgram != 0)
            {
                _gl.DeleteProgram(_shaderProgram);
                _shaderProgram = 0;
            }

            if (_axisVao != 0)
            {
                _gl.DeleteVertexArray(_axisVao);
                _axisVao = 0;
            }
            if (_axisVbo != 0)
            {
                _gl.DeleteBuffer(_axisVbo);
                _axisVbo = 0;
            }

            if (_gridVao != 0)
            {
                _gl.DeleteVertexArray(_gridVao);
                _gridVao = 0;
            }
            if (_gridVbo != 0)
            {
                _gl.DeleteBuffer(_gridVbo);
                _gridVbo = 0;
            }

            if (_overlayVao != 0)
            {
                _gl.DeleteVertexArray(_overlayVao);
                _overlayVao = 0;
            }
            if (_overlayVbo != 0)
            {
                _gl.DeleteBuffer(_overlayVbo);
                _overlayVbo = 0;
            }

            if (_textVao != 0)
            {
                _gl.DeleteVertexArray(_textVao);
                _textVao = 0;
            }
            if (_textVbo != 0)
            {
                _gl.DeleteBuffer(_textVbo);
                _textVbo = 0;
            }

            if (_overlayShaderProgram != 0)
            {
                _gl.DeleteProgram(_overlayShaderProgram);
                _overlayShaderProgram = 0;
            }
            if (_textShaderProgram != 0)
            {
                _gl.DeleteProgram(_textShaderProgram);
                _textShaderProgram = 0;
            }

            if (_cachedTextTexture != 0)
            {
                _gl.DeleteTexture(_cachedTextTexture);
                _cachedTextTexture = 0;
                _cachedTextContent = string.Empty;
            }

            if (_gizmoVao != 0)
            {
                _gl.DeleteVertexArray(_gizmoVao);
                _gizmoVao = 0;
            }
            if (_gizmoVbo != 0)
            {
                _gl.DeleteBuffer(_gizmoVbo);
                _gizmoVbo = 0;
            }
            foreach (var kvp in _gizmoTextCache)
            {
                _gl.DeleteTexture(kvp.Value.texture);
            }
            _gizmoTextCache.Clear();

            CleanupSharpBuffers();

            _gl = null;
        }

        if (_hGLRC != IntPtr.Zero)
        {
            Win32Interop.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            Win32Interop.wglDeleteContext(_hGLRC);
            _hGLRC = IntPtr.Zero;
        }

        if (_hDC != IntPtr.Zero && _hwnd != IntPtr.Zero)
        {
            Win32Interop.ReleaseDC(_hwnd, _hDC);
            _hDC = IntPtr.Zero;
        }

        if (_opengl32Handle != IntPtr.Zero)
        {
            Win32Interop.FreeLibrary(_opengl32Handle);
            _opengl32Handle = IntPtr.Zero;
        }

        _isInitialized = false;
    }

    private unsafe void InitializeShaders()
    {
        if (_gl == null) return;

        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, VertexShaderSource);
        _gl.CompileShader(vertexShader);

        _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(vertexShader);
            throw new Exception($"Vertex shader compilation failed: {infoLog}");
        }

        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, FragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(fragmentShader);
            throw new Exception($"Fragment shader compilation failed: {infoLog}");
        }

        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);
        _gl.LinkProgram(_shaderProgram);

        _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetProgramInfoLog(_shaderProgram);
            throw new Exception($"Shader program linking failed: {infoLog}");
        }

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        uint overlayVertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(overlayVertexShader, OverlayVertexShaderSource);
        _gl.CompileShader(overlayVertexShader);

        _gl.GetShader(overlayVertexShader, ShaderParameterName.CompileStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(overlayVertexShader);
            throw new Exception($"Overlay vertex shader compilation failed: {infoLog}");
        }

        uint overlayFragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(overlayFragmentShader, OverlayFragmentShaderSource);
        _gl.CompileShader(overlayFragmentShader);

        _gl.GetShader(overlayFragmentShader, ShaderParameterName.CompileStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(overlayFragmentShader);
            throw new Exception($"Overlay fragment shader compilation failed: {infoLog}");
        }

        _overlayShaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_overlayShaderProgram, overlayVertexShader);
        _gl.AttachShader(_overlayShaderProgram, overlayFragmentShader);
        _gl.LinkProgram(_overlayShaderProgram);

        _gl.GetProgram(_overlayShaderProgram, ProgramPropertyARB.LinkStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetProgramInfoLog(_overlayShaderProgram);
            throw new Exception($"Overlay shader program linking failed: {infoLog}");
        }

        _gl.DeleteShader(overlayVertexShader);
        _gl.DeleteShader(overlayFragmentShader);
    }

    private unsafe void InitializeTextShaders()
    {
        if (_gl == null) return;

        uint textVertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(textVertexShader, TextVertexShaderSource);
        _gl.CompileShader(textVertexShader);

        _gl.GetShader(textVertexShader, ShaderParameterName.CompileStatus, out int success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(textVertexShader);
            throw new Exception($"Text vertex shader compilation failed: {infoLog}");
        }

        uint textFragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(textFragmentShader, TextFragmentShaderSource);
        _gl.CompileShader(textFragmentShader);

        _gl.GetShader(textFragmentShader, ShaderParameterName.CompileStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(textFragmentShader);
            throw new Exception($"Text fragment shader compilation failed: {infoLog}");
        }

        _textShaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_textShaderProgram, textVertexShader);
        _gl.AttachShader(_textShaderProgram, textFragmentShader);
        _gl.LinkProgram(_textShaderProgram);

        _gl.GetProgram(_textShaderProgram, ProgramPropertyARB.LinkStatus, out success);
        if (success == 0)
        {
            string infoLog = _gl.GetProgramInfoLog(_textShaderProgram);
            throw new Exception($"Text shader program linking failed: {infoLog}");
        }

        _gl.DeleteShader(textVertexShader);
        _gl.DeleteShader(textFragmentShader);
    }

    private unsafe void InitializeBuffers()
    {
        if (_gl == null) return;

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private unsafe void CreateCoordinateAxisAndGrid()
    {
        if (_gl == null) return;
        CreateCoordinateAxis();
    }

    private unsafe void CreateCoordinateAxis()
    {
        if (_gl == null) return;

        const float axisLength = 1.0f;
        var axisVertices = new float[]
        {
            _pointCloudCenter.X, _pointCloudCenter.Y, _pointCloudCenter.Z, 1, 0, 0, 1,
            _pointCloudCenter.X + axisLength, _pointCloudCenter.Y, _pointCloudCenter.Z, 1, 0, 0, 1,

            _pointCloudCenter.X, _pointCloudCenter.Y, _pointCloudCenter.Z, 0, 1, 0, 1,
            _pointCloudCenter.X, _pointCloudCenter.Y + axisLength, _pointCloudCenter.Z, 0, 1, 0, 1,

            _pointCloudCenter.X, _pointCloudCenter.Y, _pointCloudCenter.Z, 0, 0, 1, 1,
            _pointCloudCenter.X, _pointCloudCenter.Y, _pointCloudCenter.Z + axisLength, 0, 0, 1, 1,
        };

        _axisVertexCount = 6;
        _axisVao = _gl.GenVertexArray();
        _axisVbo = _gl.GenBuffer();

        _gl.BindVertexArray(_axisVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _axisVbo);

        fixed (float* data = axisVertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(axisVertices.Length * sizeof(float)), data, BufferUsageARB.StaticDraw);
        }

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private unsafe void UpdateXYGrid(float gridRange)
    {
        if (_gl == null) return;

        const float gridSpacing = 0.1f;
        const float gridAlpha = 0.3f;
        const float gridR = 0.7f;
        const float gridG = 0.7f;
        const float gridB = 0.7f;

        var gridVertices = new List<float>();

        for (float y = -gridRange; y <= gridRange; y += gridSpacing)
        {
            gridVertices.Add(_pointCloudCenter.X - gridRange);
            gridVertices.Add(_pointCloudCenter.Y + y);
            gridVertices.Add(_pointCloudCenter.Z);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);

            gridVertices.Add(_pointCloudCenter.X + gridRange);
            gridVertices.Add(_pointCloudCenter.Y + y);
            gridVertices.Add(_pointCloudCenter.Z);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);
        }

        for (float x = -gridRange; x <= gridRange; x += gridSpacing)
        {
            gridVertices.Add(_pointCloudCenter.X + x);
            gridVertices.Add(_pointCloudCenter.Y - gridRange);
            gridVertices.Add(_pointCloudCenter.Z);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);

            gridVertices.Add(_pointCloudCenter.X + x);
            gridVertices.Add(_pointCloudCenter.Y + gridRange);
            gridVertices.Add(_pointCloudCenter.Z);
            gridVertices.Add(gridR);
            gridVertices.Add(gridG);
            gridVertices.Add(gridB);
            gridVertices.Add(gridAlpha);
        }

        _gridVertexCount = gridVertices.Count / 7;

        if (_gridVao == 0)
        {
            _gridVao = _gl.GenVertexArray();
            _gridVbo = _gl.GenBuffer();
        }

        _gl.BindVertexArray(_gridVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _gridVbo);

        var gridArray = gridVertices.ToArray();
        fixed (float* data = gridArray)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(gridArray.Length * sizeof(float)), data, BufferUsageARB.DynamicDraw);
        }

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private unsafe void UpdatePointCloudBuffer()
    {
        if (_gl == null || !_isInitialized || Points == null) return;

        Win32Interop.wglMakeCurrent(_hDC, _hGLRC);

        var points = Points;
        int totalPoints = points.Count;

        float minX = MinX;
        float maxX = MaxX;
        float minY = MinY;
        float maxY = MaxY;
        float minZ = MinZ;
        float maxZ = MaxZ;

        int stride = 1;
        if (_useLod && totalPoints > LodThreshold)
        {
            stride = (totalPoints + MaxDisplayPoints - 1) / MaxDisplayPoints;
            _currentLodLevel = (int)Math.Log2(stride);
        }
        else
        {
            _currentLodLevel = 0;
        }

        int estimatedCount = (totalPoints + stride - 1) / stride;
        var vertices = new float[estimatedCount * 7];
        int vertexIndex = 0;

        for (int i = 0; i < totalPoints; i += stride)
        {
            var point = points[i];

            if (point.Position.X >= minX && point.Position.X <= maxX &&
                point.Position.Y >= minY && point.Position.Y <= maxY &&
                point.Position.Z >= minZ && point.Position.Z <= maxZ)
            {
                vertices[vertexIndex++] = point.Position.X;
                vertices[vertexIndex++] = point.Position.Y;
                vertices[vertexIndex++] = point.Position.Z;
                vertices[vertexIndex++] = point.Color.X;
                vertices[vertexIndex++] = point.Color.Y;
                vertices[vertexIndex++] = point.Color.Z;
                vertices[vertexIndex++] = point.Color.W;
            }
        }

        _vertexCount = vertexIndex / 7;

        if (_vertexCount == 0) return;

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* data = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertexIndex * sizeof(float)), data, BufferUsageARB.StaticDraw);
        }
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        _needsRender = true;
    }

    #endregion
}
