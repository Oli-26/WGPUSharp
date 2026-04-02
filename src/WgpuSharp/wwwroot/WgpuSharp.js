const handles = new Map();
let nextHandle = 1;
let loopCallbackRef = null;
let loopRunning = false;
let loopFrameIndex = 0;
let loopLastTime = 0;

// Frame-scoped handles — released automatically each frame
let frameHandles = [];

function store(obj) {
    const id = nextHandle++;
    handles.set(id, obj);
    return id;
}

// Store a handle that will be auto-released at end of frame
function storeFrame(obj) {
    const id = store(obj);
    frameHandles.push(id);
    return id;
}

function get(id) {
    return handles.get(id);
}

function release(id) {
    handles.delete(id);
}

function releaseFrameHandles() {
    for (const id of frameHandles) {
        handles.delete(id);
    }
    frameHandles = [];
}

// Input state
const inputState = {
    keys: {},
    keyDownEvents: [],   // keys pressed since last poll (edge-triggered)
    ctrlKey: false,
    shiftKey: false,
    mouseX: 0, mouseY: 0,
    mouseDX: 0, mouseDY: 0,
    mouseButtons: 0,
    wheelDelta: 0,
    pointerLocked: false,
    clickEvents: [],     // [{x, y, button}] — clicks (mouseup with <5px drag)
    mouseDownEvents: [], // [{x, y, button}] — mousedown on canvas
    _mouseDownPos: null, // {x, y} at mousedown for drag detection
    _pointerLockBlocked: false, // set true to prevent pointer lock (e.g. during gizmo drag)
    boxSelectEvents: [],     // [{x1, y1, x2, y2}] — completed right-drag box selections
    _listeners: null,
};

window.WgpuSharp = {
    // Availability check
    isWebGpuSupported() {
        return !!navigator.gpu;
    },

    // Adapter
    async requestAdapter(preferHighPerformance) {
        if (!navigator.gpu) {
            throw new Error("WebGPU is not supported in this browser. Use Chrome/Edge with WebGPU enabled (chrome://flags/#enable-unsafe-webgpu).");
        }
        // On Linux, high-performance can trigger Vulkan driver bugs (Dawn shared
        // image import failures, swap chain hangs). Default to safe unless opted in.
        let adapter = null;
        if (preferHighPerformance) {
            try {
                adapter = await navigator.gpu.requestAdapter({ powerPreference: "high-performance" });
            } catch (_) { /* driver issues — fall back */ }
        }
        if (!adapter) {
            adapter = await navigator.gpu.requestAdapter();
        }
        if (!adapter) {
            throw new Error("Failed to get GPU adapter. On Linux, enable WebGPU via chrome://flags/#enable-unsafe-webgpu or launch with: --enable-features=Vulkan --enable-unsafe-webgpu");
        }
        return store(adapter);
    },

    async getAdapterInfo(adapterId) {
        const adapter = get(adapterId);
        try {
            let info = null;
            // Try all known ways to get adapter info
            if (adapter.info && typeof adapter.info === 'object') {
                info = adapter.info;
            } else if (typeof adapter.requestAdapterInfo === 'function') {
                info = await adapter.requestAdapterInfo();
            }
            if (!info) return { vendor: "", architecture: "", device: "", description: "" };
            return {
                vendor: info.vendor || "",
                architecture: info.architecture || "",
                device: info.device || "",
                description: info.description || "",
            };
        } catch (e) {
            console.warn('[WgpuSharp] getAdapterInfo failed:', e);
            return { vendor: "", architecture: "", device: "", description: "" };
        }
    },

    getAdapterFeatures(adapterId) {
        const adapter = get(adapterId);
        return Array.from(adapter.features || []);
    },

    getAdapterLimits(adapterId) {
        const adapter = get(adapterId);
        const l = adapter.limits;
        return {
            maxTextureDimension2D: l.maxTextureDimension2D,
            maxTextureArrayLayers: l.maxTextureArrayLayers,
            maxBindGroups: l.maxBindGroups,
            maxBufferSize: l.maxBufferSize,
            maxVertexBuffers: l.maxVertexBuffers,
            maxComputeWorkgroupSizeX: l.maxComputeWorkgroupSizeX,
            maxComputeWorkgroupSizeY: l.maxComputeWorkgroupSizeY,
            maxComputeWorkgroupSizeZ: l.maxComputeWorkgroupSizeZ,
        };
    },

    // Device
    async requestDevice(adapterId) {
        const adapter = get(adapterId);
        const device = await adapter.requestDevice();
        return store(device);
    },

    registerDeviceLostCallback(deviceId, dotNetRef) {
        const device = get(deviceId);
        device.lost.then(info => {
            console.error("WgpuSharp: GPU device lost —", info.message, "(reason:", info.reason + ")");
            try {
                dotNetRef.invokeMethodAsync("OnDeviceLost", info.reason || "unknown", info.message || "");
            } catch (_) {}
        });
    },

    // Shader compilation with error details
    createShaderModuleWithErrors(deviceId, wgslCode) {
        const device = get(deviceId);
        const module = device.createShaderModule({ code: wgslCode });
        // getCompilationInfo is async but module is usable immediately.
        // We return the handle and let C# check compilation separately.
        return store(module);
    },

    async getShaderCompilationInfo(moduleId) {
        const module = get(moduleId);
        if (!module.getCompilationInfo) return { messages: [] };
        const info = await module.getCompilationInfo();
        return {
            messages: info.messages.map(m => ({
                type: m.type,        // "error", "warning", "info"
                message: m.message,
                lineNum: m.lineNum,
                linePos: m.linePos,
                offset: m.offset,
                length: m.length,
            })),
        };
    },

    // Canvas context
    configureCanvas(deviceId, canvasId, format) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            throw new Error(`Canvas element '${canvasId}' not found.`);
        }
        const ctx = canvas.getContext("webgpu");
        const device = get(deviceId);
        ctx.configure({
            device: device,
            format: format,
            alphaMode: "premultiplied",
        });
        return store(ctx);
    },

    getPreferredCanvasFormat() {
        return navigator.gpu.getPreferredCanvasFormat();
    },

    getCanvasSize(canvasId) {
        const canvas = document.getElementById(canvasId);
        return { width: canvas.width, height: canvas.height };
    },

    getCanvasDisplaySize(canvasId) {
        const canvas = document.getElementById(canvasId);
        const dpr = window.devicePixelRatio || 1;
        // Round to even numbers to avoid Vulkan texture alignment issues
        // at fractional DPR (known Dawn bug on Linux with 125%/150% scaling)
        let w = Math.round(canvas.clientWidth * dpr);
        let h = Math.round(canvas.clientHeight * dpr);
        if (dpr % 1 !== 0) { w = w & ~1; h = h & ~1; } // force even
        return { width: w, height: h };
    },

    setCanvasSize(canvasId, width, height) {
        const canvas = document.getElementById(canvasId);
        canvas.width = width;
        canvas.height = height;
    },

    getCurrentTexture(contextId) {
        const ctx = get(contextId);
        const texture = ctx.getCurrentTexture();
        return storeFrame(texture);
    },

    getTextureSize(textureId) {
        const texture = get(textureId);
        return { width: texture.width, height: texture.height };
    },

    createTextureView(textureId) {
        const texture = get(textureId);
        const view = texture.createView();
        return storeFrame(view);
    },

    // Shader module
    createShaderModule(deviceId, wgslCode) {
        const device = get(deviceId);
        const module = device.createShaderModule({ code: wgslCode });
        return store(module);
    },

    // Buffer
    createBuffer(deviceId, size, usage, mappedAtCreation) {
        const device = get(deviceId);
        const buffer = device.createBuffer({
            size: size,
            usage: usage,
            mappedAtCreation: mappedAtCreation,
        });
        return store(buffer);
    },

    writeBuffer(deviceId, bufferId, dataBase64) {
        const device = get(deviceId);
        const buffer = get(bufferId);
        const bytes = Uint8Array.from(atob(dataBase64), c => c.charCodeAt(0));
        device.queue.writeBuffer(buffer, 0, bytes);
    },

    // Texture
    createTexture(deviceId, descriptor) {
        const device = get(deviceId);
        const texture = device.createTexture({
            size: descriptor.size,
            format: descriptor.format,
            usage: descriptor.usage,
            sampleCount: descriptor.sampleCount || 1,
        });
        return store(texture);
    },

    createTextureViewWithDescriptor(textureId, descriptor) {
        const texture = get(textureId);
        const view = texture.createView(descriptor || {});
        return store(view);
    },

    writeTexture(deviceId, textureId, dataBase64, width, height) {
        const device = get(deviceId);
        const texture = get(textureId);
        const bytes = Uint8Array.from(atob(dataBase64), c => c.charCodeAt(0));
        device.queue.writeTexture(
            { texture: texture },
            bytes,
            { bytesPerRow: width * 4, rowsPerImage: height },
            { width: width, height: height },
        );
    },

    // Create texture from encoded image bytes (PNG, JPG, etc.)
    async createTextureFromImage(deviceId, imageBase64, generateMipmaps) {
        const device = get(deviceId);
        const bytes = Uint8Array.from(atob(imageBase64), c => c.charCodeAt(0));
        const blob = new Blob([bytes]);
        const bitmap = await createImageBitmap(blob);

        const texture = device.createTexture({
            size: [bitmap.width, bitmap.height],
            format: "rgba8unorm",
            usage: GPUTextureUsage.TEXTURE_BINDING |
                   GPUTextureUsage.COPY_DST |
                   GPUTextureUsage.RENDER_ATTACHMENT,
        });

        device.queue.copyExternalImageToTexture(
            { source: bitmap },
            { texture: texture },
            [bitmap.width, bitmap.height],
        );

        bitmap.close();
        return store(texture);
    },

    // Sampler
    createSampler(deviceId, descriptor) {
        const device = get(deviceId);
        const sampler = device.createSampler({
            magFilter: descriptor.magFilter || "linear",
            minFilter: descriptor.minFilter || "linear",
            addressModeU: descriptor.addressModeU || "clamp-to-edge",
            addressModeV: descriptor.addressModeV || "clamp-to-edge",
        });
        return store(sampler);
    },

    // Bind group
    createBindGroup(deviceId, pipelineId, groupIndex, entries) {
        const device = get(deviceId);
        const pipeline = get(pipelineId);
        const layout = pipeline.getBindGroupLayout(groupIndex);

        const gpuEntries = entries.map(e => {
            const entry = { binding: e.binding };
            if (e.bufferId !== undefined && e.bufferId !== null) {
                entry.resource = {
                    buffer: get(e.bufferId),
                    offset: e.offset || 0,
                    size: e.size || undefined,
                };
            } else if (e.samplerId !== undefined && e.samplerId !== null) {
                entry.resource = get(e.samplerId);
            } else if (e.textureViewId !== undefined && e.textureViewId !== null) {
                entry.resource = get(e.textureViewId);
            }
            return entry;
        });

        const bindGroup = device.createBindGroup({
            layout: layout,
            entries: gpuEntries,
        });
        return store(bindGroup);
    },

    setBindGroup(passId, groupIndex, bindGroupId) {
        const pass = get(passId);
        pass.setBindGroup(groupIndex, get(bindGroupId));
    },

    // Render pipeline
    createRenderPipeline(deviceId, descriptor) {
        const device = get(deviceId);

        const pipelineDescriptor = {
            layout: "auto",
            vertex: {
                module: get(descriptor.vertexModuleId),
                entryPoint: descriptor.vertexEntryPoint,
                buffers: descriptor.vertexBuffers ? descriptor.vertexBuffers.map(b => ({
                    arrayStride: b.arrayStride,
                    stepMode: b.stepMode || "vertex",
                    attributes: b.attributes.map(a => ({
                        shaderLocation: a.shaderLocation,
                        offset: a.offset,
                        format: a.format,
                    })),
                })) : [],
            },
            fragment: {
                module: get(descriptor.fragmentModuleId),
                entryPoint: descriptor.fragmentEntryPoint,
                targets: descriptor.colorTargets.map(t => {
                    const target = { format: t.format };
                    if (t.blend) { target.blend = t.blend; }
                    return target;
                }),
            },
            primitive: {
                topology: descriptor.primitiveTopology || "triangle-list",
                cullMode: descriptor.cullMode || "none",
            },
        };

        if (descriptor.depthStencil) {
            pipelineDescriptor.depthStencil = {
                format: descriptor.depthStencil.format,
                depthWriteEnabled: descriptor.depthStencil.depthWriteEnabled,
                depthCompare: descriptor.depthStencil.depthCompare,
            };
        }

        if (descriptor.multisample) {
            pipelineDescriptor.multisample = {
                count: descriptor.multisample.count,
            };
        }

        const pipeline = device.createRenderPipeline(pipelineDescriptor);
        return store(pipeline);
    },

    // Command encoder
    createCommandEncoder(deviceId) {
        const device = get(deviceId);
        const encoder = device.createCommandEncoder();
        return storeFrame(encoder);
    },

    beginRenderPass(encoderId, descriptor) {
        const encoder = get(encoderId);

        const colorAttachments = descriptor.colorAttachments.map(a => {
            const attachment = {
                view: get(a.viewId),
                clearValue: a.clearValue,
                loadOp: a.loadOp,
                storeOp: a.storeOp,
            };
            if (a.resolveTargetId !== undefined && a.resolveTargetId !== null) {
                attachment.resolveTarget = get(a.resolveTargetId);
            }
            return attachment;
        });

        const passDescriptor = { colorAttachments };

        if (descriptor.depthStencilAttachment) {
            const ds = descriptor.depthStencilAttachment;
            passDescriptor.depthStencilAttachment = {
                view: get(ds.viewId),
                depthClearValue: ds.depthClearValue !== undefined ? ds.depthClearValue : 1.0,
                depthLoadOp: ds.depthLoadOp || "clear",
                depthStoreOp: ds.depthStoreOp || "store",
            };
        }

        const pass = encoder.beginRenderPass(passDescriptor);
        return storeFrame(pass);
    },

    setPipeline(passId, pipelineId) {
        const pass = get(passId);
        pass.setPipeline(get(pipelineId));
    },

    setVertexBuffer(passId, slot, bufferId) {
        const pass = get(passId);
        pass.setVertexBuffer(slot, get(bufferId));
    },

    setIndexBuffer(passId, bufferId, format) {
        const pass = get(passId);
        pass.setIndexBuffer(get(bufferId), format);
    },

    draw(passId, vertexCount, instanceCount, firstVertex, firstInstance) {
        const pass = get(passId);
        pass.draw(vertexCount, instanceCount, firstVertex, firstInstance);
    },

    drawIndexed(passId, indexCount, instanceCount, firstIndex, baseVertex, firstInstance) {
        const pass = get(passId);
        pass.drawIndexed(indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
    },

    drawIndirect(passId, indirectBufferId, indirectOffset) {
        const pass = get(passId);
        pass.drawIndirect(get(indirectBufferId), indirectOffset);
    },

    drawIndexedIndirect(passId, indirectBufferId, indirectOffset) {
        const pass = get(passId);
        pass.drawIndexedIndirect(get(indirectBufferId), indirectOffset);
    },

    endPass(passId) {
        const pass = get(passId);
        pass.end();
    },

    finishEncoder(encoderId) {
        const encoder = get(encoderId);
        const commandBuffer = encoder.finish();
        return storeFrame(commandBuffer);
    },

    submit(deviceId, commandBufferIds) {
        const device = get(deviceId);
        const commandBuffers = commandBufferIds.map(id => get(id));
        device.queue.submit(commandBuffers);
    },

    // Compute pipeline
    createComputePipeline(deviceId, descriptor) {
        const device = get(deviceId);
        const pipeline = device.createComputePipeline({
            layout: "auto",
            compute: {
                module: get(descriptor.moduleId),
                entryPoint: descriptor.entryPoint,
            },
        });
        return store(pipeline);
    },

    beginComputePass(encoderId) {
        const encoder = get(encoderId);
        const pass = encoder.beginComputePass();
        return storeFrame(pass);
    },

    dispatchWorkgroups(passId, x, y, z) {
        const pass = get(passId);
        pass.dispatchWorkgroups(x, y, z);
    },

    // Copy buffer to buffer (for read-back via mapping)
    copyBufferToBuffer(encoderId, srcId, srcOffset, dstId, dstOffset, size) {
        const encoder = get(encoderId);
        encoder.copyBufferToBuffer(get(srcId), srcOffset, get(dstId), dstOffset, size);
    },

    // Map buffer and read data back
    async mapReadBuffer(bufferId) {
        const buffer = get(bufferId);
        await buffer.mapAsync(GPUMapMode.READ);
        const data = new Uint8Array(buffer.getMappedRange()).slice();
        buffer.unmap();
        // Convert to base64 for transfer to C#
        let binary = "";
        const len = data.length;
        for (let i = 0; i < len; i++) {
            binary += String.fromCharCode(data[i]);
        }
        return btoa(binary);
    },

    // Create bind group for compute pipeline (same logic as render)
    createComputeBindGroup(deviceId, pipelineId, groupIndex, entries) {
        const device = get(deviceId);
        const pipeline = get(pipelineId);
        const layout = pipeline.getBindGroupLayout(groupIndex);

        const gpuEntries = entries.map(e => {
            const entry = { binding: e.binding };
            if (e.bufferId !== undefined && e.bufferId !== null) {
                entry.resource = {
                    buffer: get(e.bufferId),
                    offset: e.offset || 0,
                    size: e.size || undefined,
                };
            } else if (e.samplerId !== undefined && e.samplerId !== null) {
                entry.resource = get(e.samplerId);
            } else if (e.textureViewId !== undefined && e.textureViewId !== null) {
                entry.resource = get(e.textureViewId);
            }
            return entry;
        });

        const bindGroup = device.createBindGroup({
            layout: layout,
            entries: gpuEntries,
        });
        return store(bindGroup);
    },

    // GpuLoop — requestAnimationFrame-based render loop
    // Waits for each frame callback to complete before scheduling the next,
    // preventing callback pile-up when the tab is backgrounded.
    startLoop(dotNetRef) {
        loopCallbackRef = dotNetRef;
        loopRunning = true;
        loopFrameIndex = 0;
        loopLastTime = performance.now();

        async function tick(now) {
            if (!loopRunning) return;
            const dt = Math.min((now - loopLastTime) / 1000.0, 0.1);
            loopLastTime = now;
            try {
                await loopCallbackRef.invokeMethodAsync("OnFrame", loopFrameIndex, dt);
            } catch (e) {
                if (loopRunning) console.error("WgpuSharp frame error:", e);
            }
            // Release per-frame handles (textures, views, encoders, command buffers)
            releaseFrameHandles();
            // Reset per-frame mouse deltas
            inputState.mouseDX = 0;
            inputState.mouseDY = 0;
            inputState.wheelDelta = 0;
            loopFrameIndex++;
            if (loopRunning) requestAnimationFrame(tick);
        }

        requestAnimationFrame(tick);
    },

    stopLoop() {
        loopRunning = false;
        loopCallbackRef = null;
    },

    // Batched command execution — single interop call per frame
    // Commands: [[opCode, resultSlot, ...args], ...]
    // Negative arg values reference result slots: actual = results[-(arg+1)]
    // resultSlot: -1 = no result, >= 0 = store result at that index
    executeBatch(commands, bufferWrites) {
        const results = [];

        function r(id) {
            return id < 0 ? results[-(id + 1)] : id;
        }

        // Pre-process buffer writes (base64 → Uint8Array)
        const writes = {};
        if (bufferWrites) {
            for (const w of bufferWrites) {
                // Use Uint8Array.from for cleaner decoding
                const bin = atob(w.data);
                const arr = new Uint8Array(bin.length);
                // Unrolled loop — process 4 bytes at a time
                const len = bin.length;
                let i = 0;
                for (; i + 3 < len; i += 4) {
                    arr[i]     = bin.charCodeAt(i);
                    arr[i + 1] = bin.charCodeAt(i + 1);
                    arr[i + 2] = bin.charCodeAt(i + 2);
                    arr[i + 3] = bin.charCodeAt(i + 3);
                }
                for (; i < len; i++) arr[i] = bin.charCodeAt(i);
                writes[w.key] = arr;
            }
        }

        for (const cmd of commands) {
            const op = cmd[0];
            const slot = cmd[1];
            let result = undefined;

            switch (op) {
                case 0: { // getCurrentTexture(contextId)
                    const ctx = get(r(cmd[2]));
                    result = storeFrame(ctx.getCurrentTexture());
                    break;
                }
                case 1: { // createTextureView(textureId)
                    result = storeFrame(get(r(cmd[2])).createView());
                    break;
                }
                case 2: { // createCommandEncoder(deviceId)
                    result = storeFrame(get(r(cmd[2])).createCommandEncoder());
                    break;
                }
                case 3: { // beginRenderPass(encoderId, colorAttachments, depthAttachment)
                    const encoder = get(r(cmd[2]));
                    const colorAttachments = cmd[3].map(a => {
                        const attachment = {
                            view: get(r(a.viewId)),
                            clearValue: a.clearValue,
                            loadOp: a.loadOp,
                            storeOp: a.storeOp,
                        };
                        if (a.resolveTargetId !== undefined && a.resolveTargetId !== null) {
                            attachment.resolveTarget = get(r(a.resolveTargetId));
                        }
                        return attachment;
                    });
                    const desc = { colorAttachments };
                    if (cmd[4]) {
                        desc.depthStencilAttachment = {
                            view: get(r(cmd[4].viewId)),
                            depthClearValue: cmd[4].depthClearValue !== undefined ? cmd[4].depthClearValue : 1.0,
                            depthLoadOp: cmd[4].depthLoadOp || "clear",
                            depthStoreOp: cmd[4].depthStoreOp || "store",
                        };
                    }
                    result = storeFrame(encoder.beginRenderPass(desc));
                    break;
                }
                case 4: { // setPipeline(passId, pipelineId)
                    get(r(cmd[2])).setPipeline(get(r(cmd[3])));
                    break;
                }
                case 5: { // setBindGroup(passId, groupIndex, bindGroupId)
                    get(r(cmd[2])).setBindGroup(cmd[3], get(r(cmd[4])));
                    break;
                }
                case 6: { // setVertexBuffer(passId, slot, bufferId)
                    get(r(cmd[2])).setVertexBuffer(cmd[3], get(r(cmd[4])));
                    break;
                }
                case 7: { // setIndexBuffer(passId, bufferId, format)
                    get(r(cmd[2])).setIndexBuffer(get(r(cmd[3])), cmd[4]);
                    break;
                }
                case 8: { // draw(passId, vertexCount, instanceCount, firstVertex, firstInstance)
                    get(r(cmd[2])).draw(cmd[3], cmd[4], cmd[5], cmd[6]);
                    break;
                }
                case 9: { // drawIndexed(passId, indexCount, instanceCount, firstIndex, baseVertex, firstInstance)
                    get(r(cmd[2])).drawIndexed(cmd[3], cmd[4], cmd[5], cmd[6], cmd[7]);
                    break;
                }
                case 10: { // endPass(passId)
                    get(r(cmd[2])).end();
                    break;
                }
                case 11: { // finishEncoder(encoderId)
                    result = storeFrame(get(r(cmd[2])).finish());
                    break;
                }
                case 12: { // submit(deviceId, commandBufferIds)
                    const device = get(r(cmd[2]));
                    const bufs = cmd[3].map(id => get(r(id)));
                    device.queue.submit(bufs);
                    break;
                }
                case 13: { // writeBuffer(deviceId, bufferId, writeKey)
                    const device = get(r(cmd[2]));
                    const buffer = get(r(cmd[3]));
                    device.queue.writeBuffer(buffer, 0, writes[cmd[4]]);
                    break;
                }
                case 14: { // beginComputePass(encoderId)
                    result = storeFrame(get(r(cmd[2])).beginComputePass());
                    break;
                }
                case 15: { // dispatchWorkgroups(passId, x, y, z)
                    get(r(cmd[2])).dispatchWorkgroups(cmd[3], cmd[4], cmd[5]);
                    break;
                }
                case 16: { // releaseHandle(id)
                    release(r(cmd[2]));
                    break;
                }
                case 17: { // drawIndirect(passId, bufferId, offset)
                    get(r(cmd[2])).drawIndirect(get(r(cmd[3])), cmd[4]);
                    break;
                }
                case 18: { // drawIndexedIndirect(passId, bufferId, offset)
                    get(r(cmd[2])).drawIndexedIndirect(get(r(cmd[3])), cmd[4]);
                    break;
                }
            }

            if (slot >= 0) {
                results[slot] = result;
            }
        }

        return results;
    },

    // Input
    initInput(canvasId) {
        const canvas = document.getElementById(canvasId);
        if (inputState._listeners) return; // already initialized

        const onKeyDown = (e) => {
            inputState.keys[e.code] = true;
            inputState.ctrlKey = e.ctrlKey || e.metaKey;
            inputState.shiftKey = e.shiftKey;
            // Record edge event (only if not in a text input)
            const tag = e.target?.tagName;
            if (tag !== 'INPUT' && tag !== 'TEXTAREA' && tag !== 'SELECT') {
                inputState.keyDownEvents.push(e.code);
                // Prevent browser defaults for editor shortcuts
                if (e.code === 'Delete' || e.code === 'Backspace' || e.code === 'KeyF' || e.code === 'KeyG' || e.code === 'F3'
                    || e.code === 'KeyW' || e.code === 'KeyE' || e.code === 'KeyR'
                    || ((e.ctrlKey || e.metaKey) && (e.code === 'KeyD' || e.code === 'KeyZ' || e.code === 'KeyY' || e.code === 'KeyA' || e.code === 'KeyC' || e.code === 'KeyV'))) {
                    e.preventDefault();
                }
            }
        };
        const onKeyUp = (e) => {
            inputState.keys[e.code] = false;
            inputState.ctrlKey = e.ctrlKey || e.metaKey;
            inputState.shiftKey = e.shiftKey;
        };
        const onMouseMove = (e) => {
            const dpr = window.devicePixelRatio || 1;
            if (inputState.pointerLocked) {
                inputState.mouseDX += e.movementX;
                inputState.mouseDY += e.movementY;
            } else if (inputState._mouseDownPos && !inputState.pointerLocked) {
                // Lock pointer when user starts dragging (>3px from mousedown)
                // But not if pointer lock is blocked (e.g. during gizmo drag)
                if (!inputState._pointerLockBlocked) {
                    const rect = canvas.getBoundingClientRect();
                    const mx = e.clientX - rect.left, my = e.clientY - rect.top;
                    const dx = mx - inputState._mouseDownPos.x, dy = my - inputState._mouseDownPos.y;
                    if (dx * dx + dy * dy > 9) {
                        try { canvas.requestPointerLock(); } catch (_) {}
                    }
                }
            }
            const rect = canvas.getBoundingClientRect();
            inputState.mouseX = (e.clientX - rect.left) * dpr;
            inputState.mouseY = (e.clientY - rect.top) * dpr;
        };
        const onMouseDown = (e) => {
            const dpr = window.devicePixelRatio || 1;
            inputState.mouseButtons |= (1 << e.button);
            if (!inputState.pointerLocked) {
                const rect = canvas.getBoundingClientRect();
                const cssX = e.clientX - rect.left, cssY = e.clientY - rect.top;
                inputState._mouseDownPos = { x: cssX, y: cssY, button: e.button };
                // Store buffer-pixel coordinates for C# raycasting
                inputState.mouseDownEvents.push({ x: cssX * dpr, y: cssY * dpr, button: e.button });
            }
        };
        const onMouseUp = (e) => {
            const dpr = window.devicePixelRatio || 1;
            inputState.mouseButtons &= ~(1 << e.button);
            // Detect click (not drag) — mouseup within 5px of mousedown (CSS pixels)
            if (inputState._mouseDownPos && inputState._mouseDownPos.button === e.button) {
                const rect = canvas.getBoundingClientRect();
                const ux = e.clientX - rect.left, uy = e.clientY - rect.top;
                const dx = ux - inputState._mouseDownPos.x, dy = uy - inputState._mouseDownPos.y;
                if (dx * dx + dy * dy < 25) {
                    inputState.clickEvents.push({ x: ux * dpr, y: uy * dpr, button: e.button });
                } else if (e.button === 2) {
                    // Right-drag box select (buffer pixels)
                    const sx = inputState._mouseDownPos.x * dpr, sy = inputState._mouseDownPos.y * dpr;
                    inputState.boxSelectEvents.push({
                        x1: Math.min(sx, ux * dpr), y1: Math.min(sy, uy * dpr),
                        x2: Math.max(sx, ux * dpr), y2: Math.max(sy, uy * dpr)
                    });
                }
            }
            inputState._mouseDownPos = null;
            // Release pointer lock on mouse up (so it only stays locked while dragging)
            if (inputState.pointerLocked) {
                document.exitPointerLock();
            }
        };
        const onWheel = (e) => { inputState.wheelDelta += e.deltaY; e.preventDefault(); };
        const onLockChange = () => {
            inputState.pointerLocked = document.pointerLockElement === canvas;
        };

        window.addEventListener("keydown", onKeyDown);
        window.addEventListener("keyup", onKeyUp);
        canvas.addEventListener("mousemove", onMouseMove);
        canvas.addEventListener("mousedown", onMouseDown);
        canvas.addEventListener("mouseup", onMouseUp);
        canvas.addEventListener("wheel", onWheel, { passive: false });
        canvas.addEventListener("contextmenu", (e) => e.preventDefault());
        document.addEventListener("pointerlockchange", onLockChange);

        inputState._listeners = { onKeyDown, onKeyUp, onMouseMove, onMouseDown, onMouseUp, onWheel, onLockChange, canvas };
    },

    getInputState() {
        // Build active keys array without Object.keys().filter() overhead
        const activeKeys = [];
        for (const k in inputState.keys) {
            if (inputState.keys[k]) activeKeys.push(k);
        }
        // Swap event arrays instead of slice+clear (avoids copying)
        const kd = inputState.keyDownEvents;
        const ce = inputState.clickEvents;
        const md = inputState.mouseDownEvents;
        const be = inputState.boxSelectEvents;
        inputState.keyDownEvents = [];
        inputState.clickEvents = [];
        inputState.mouseDownEvents = [];
        inputState.boxSelectEvents = [];
        const state = {
            keys: activeKeys,
            keyDownEvents: kd,
            clickEvents: ce,
            mouseDownEvents: md,
            boxSelectEvents: be,
            ctrlKey: inputState.ctrlKey,
            shiftKey: inputState.shiftKey,
            mouseX: inputState.mouseX,
            mouseY: inputState.mouseY,
            mouseDX: inputState.mouseDX,
            mouseDY: inputState.mouseDY,
            mouseButtons: inputState.mouseButtons,
            wheelDelta: inputState.wheelDelta,
            pointerLocked: inputState.pointerLocked,
        };
        return state;
    },

    isKeyDown(code) {
        return !!inputState.keys[code];
    },

    setPointerLockBlocked(blocked) {
        inputState._pointerLockBlocked = blocked;
    },

    disposeInput() {
        if (!inputState._listeners) return;
        const l = inputState._listeners;
        window.removeEventListener("keydown", l.onKeyDown);
        window.removeEventListener("keyup", l.onKeyUp);
        l.canvas.removeEventListener("mousemove", l.onMouseMove);
        l.canvas.removeEventListener("mousedown", l.onMouseDown);
        l.canvas.removeEventListener("mouseup", l.onMouseUp);
        l.canvas.removeEventListener("wheel", l.onWheel);
        document.removeEventListener("pointerlockchange", l.onLockChange);
        if (document.pointerLockElement === l.canvas) document.exitPointerLock();
        inputState._listeners = null;
        inputState.keys = {};
    },

    // Cleanup — release handle from map and optionally destroy the GPU object
    destroyHandle(id) {
        const obj = handles.get(id);
        if (obj && typeof obj.destroy === "function") {
            try { obj.destroy(); } catch (_) {}
        }
        handles.delete(id);
    },

    releaseHandle(id) {
        release(id);
    },
};
