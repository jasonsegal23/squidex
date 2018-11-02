/*
 * Squidex Headless CMS
 *
 * @license
 * Copyright (c) Squidex UG (haftungsbeschränkt). All rights reserved.
 */

// tslint:disable:prefer-for-of

import { ChangeDetectionStrategy, ChangeDetectorRef, Component, forwardRef } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

import {
    AppsState,
    AssetDto,
    AssetsService,
    DialogModel,
    ImmutableArray,
    LocalStoreService,
    Types
} from '@app/shared';

export const SQX_ASSETS_EDITOR_CONTROL_VALUE_ACCESSOR: any = {
    provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => AssetsEditorComponent), multi: true
};

@Component({
    selector: 'sqx-assets-editor',
    styleUrls: ['./assets-editor.component.scss'],
    templateUrl: './assets-editor.component.html',
    providers: [SQX_ASSETS_EDITOR_CONTROL_VALUE_ACCESSOR],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class AssetsEditorComponent implements ControlValueAccessor {
    private callChange = (v: any) => { /* NOOP */ };
    private callTouched = () => { /* NOOP */ };

    public assetsDialog = new DialogModel();

    public newAssets = ImmutableArray.empty<File>();
    public oldAssets = ImmutableArray.empty<AssetDto>();

    public isListView = false;
    public isDisabled = false;

    constructor(
        private readonly appsState: AppsState,
        private readonly assetsService: AssetsService,
        private readonly changeDetector: ChangeDetectorRef,
        private readonly localStore: LocalStoreService
    ) {
        this.isListView = this.localStore.get('assetView') === 'List';
    }

    public writeValue(obj: any) {
        if (Types.isArrayOfString(obj)) {
            if (!Types.isEquals(obj, this.oldAssets.map(x => x.id).values)) {
                const assetIds: string[] = obj;

                this.assetsService.getAssets(this.appsState.appName, 0, 0, undefined, undefined, obj)
                    .subscribe(dtos => {
                        this.setAssets(ImmutableArray.of(assetIds.map(id => dtos.items.find(x => x.id === id)!).filter(a => !!a)));

                        if (this.oldAssets.length !== assetIds.length) {
                            this.updateValue();
                        }
                    }, () => {
                        this.setAssets(ImmutableArray.empty());
                    });
            }
        } else {
            this.setAssets(ImmutableArray.empty());
        }
    }

    public setAssets(asset: ImmutableArray<AssetDto>) {
        this.oldAssets = asset;

        this.changeDetector.markForCheck();
    }

    public setDisabledState(isDisabled: boolean): void {
        this.isDisabled = isDisabled;

        this.changeDetector.markForCheck();
    }

    public registerOnChange(fn: any) {
        this.callChange = fn;
    }

    public registerOnTouched(fn: any) {
        this.callTouched = fn;
    }

    public addFiles(files: FileList) {
        for (let i = 0; i < files.length; i++) {
            this.newAssets = this.newAssets.pushFront(files[i]);
        }
    }

    public selectAssets(assets: AssetDto[]) {
        for (let asset of assets) {
            this.oldAssets = this.oldAssets.push(asset);
        }

        if (assets.length > 0) {
            this.updateValue();
        }

        this.assetsDialog.hide();
    }

    public addAsset(file: File, asset: AssetDto) {
        if (asset && file) {
            this.newAssets = this.newAssets.remove(file);
            this.oldAssets = this.oldAssets.pushFront(asset);

            this.updateValue();
        }
    }

    public removeLoadedAsset(asset: AssetDto) {
        if (asset) {
            this.oldAssets = this.oldAssets.remove(asset);

            this.updateValue();
        }
    }

    public removeLoadingAsset(file: File) {
        this.newAssets = this.newAssets.remove(file);
    }

    public changeView(isListView: boolean) {
        this.localStore.set('assetView', isListView ? 'List' : 'Grid');

        this.isListView = isListView;
    }

    public sortAssets(assets: AssetDto[]) {
        if (assets) {
            this.oldAssets = ImmutableArray.of(assets);

            this.updateValue();
        }
    }

    private updateValue() {
        let ids: string[] | null = this.oldAssets.values.map(x => x.id);

        if (ids.length === 0) {
            ids = null;
        }

        this.callTouched();
        this.callChange(ids);

        this.changeDetector.markForCheck();
    }
}