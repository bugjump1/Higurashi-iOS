#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);

static UIViewController *HigurashiTopViewController(void)
{
    UIWindow *window = nil;
    for (UIScene *scene in UIApplication.sharedApplication.connectedScenes)
    {
        if (scene.activationState != UISceneActivationStateUnattached &&
            [scene isKindOfClass:[UIWindowScene class]])
        {
            NSArray<UIWindow *> *windows = ((UIWindowScene *)scene).windows;
            for (UIWindow *candidate in windows)
            {
                if (candidate.isKeyWindow)
                {
                    window = candidate;
                    break;
                }
            }
            if (window == nil) window = windows.firstObject;
        }
        if (window != nil) break;
    }

    UIViewController *controller = window.rootViewController;
    while (controller.presentedViewController != nil)
    {
        controller = controller.presentedViewController;
    }
    return controller;
}

@interface HigurashiDataPackPickerDelegate : NSObject <UIDocumentPickerDelegate>
@property(nonatomic, copy) NSString *destinationPath;
@property(nonatomic, copy) NSString *callbackGameObject;
@end

static HigurashiDataPackPickerDelegate *HigurashiActivePickerDelegate;

@implementation HigurashiDataPackPickerDelegate

- (void)finishWithMethod:(const char *)method message:(NSString *)message
{
    UnitySendMessage(
        self.callbackGameObject.UTF8String,
        method,
        (message ?: @"").UTF8String);
    HigurashiActivePickerDelegate = nil;
}

- (void)documentPicker:(UIDocumentPickerViewController *)controller
didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls
{
    NSURL *sourceURL = urls.firstObject;
    if (sourceURL == nil)
    {
        [self finishWithMethod:"OnDataPackPickerFailed" message:@"没有选中数据包。"];
        return;
    }

    NSString *destination = self.destinationPath;
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        BOOL scoped = [sourceURL startAccessingSecurityScopedResource];
        NSError *error = nil;
        NSFileManager *files = NSFileManager.defaultManager;
        [files removeItemAtPath:destination error:nil];
        [files copyItemAtURL:sourceURL toURL:[NSURL fileURLWithPath:destination] error:&error];
        if (scoped) [sourceURL stopAccessingSecurityScopedResource];

        dispatch_async(dispatch_get_main_queue(), ^{
            if (error != nil)
            {
                [self finishWithMethod:"OnDataPackPickerFailed"
                               message:[@"复制数据包失败：" stringByAppendingString:error.localizedDescription]];
                return;
            }
            [self finishWithMethod:"OnDataPackPicked" message:destination];
        });
    });
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller
{
    [self finishWithMethod:"OnDataPackPickerFailed" message:@"已取消选择数据包。"];
}

@end

extern "C" void Higurashi_ShowDataPackPicker(
    const char *destinationPath,
    const char *callbackGameObject)
{
    NSString *destination = [NSString stringWithUTF8String:destinationPath ?: ""];
    NSString *callback = [NSString stringWithUTF8String:callbackGameObject ?: ""];
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *presenter = HigurashiTopViewController();
        if (presenter == nil)
        {
            UnitySendMessage(callback.UTF8String, "OnDataPackPickerFailed", "无法打开 iOS 文件选择器。");
            return;
        }

        HigurashiDataPackPickerDelegate *delegate = [HigurashiDataPackPickerDelegate new];
        delegate.destinationPath = destination;
        delegate.callbackGameObject = callback;
        HigurashiActivePickerDelegate = delegate;

        UIDocumentPickerViewController *picker =
            [[UIDocumentPickerViewController alloc]
                initForOpeningContentTypes:@[UTTypeZIP]
                asCopy:NO];
        picker.delegate = delegate;
        picker.allowsMultipleSelection = NO;
        picker.modalPresentationStyle = UIModalPresentationFormSheet;
        [presenter presentViewController:picker animated:YES completion:nil];
    });
}
